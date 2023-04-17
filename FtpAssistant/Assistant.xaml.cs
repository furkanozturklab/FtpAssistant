using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FtpAssistant
{
    /// <summary>
    /// Interaction logic for Assistant.xaml
    /// </summary>
    public partial class Assistant : Window
    {


        private Task watcher, other;
        Stopwatch stopwatch = new Stopwatch();
        private List<FileInfo> files = new List<FileInfo>(); // File listesi
        private int selectedId = -1, totalChange = 0;
        private bool watcherStatus = false, ftpConnection = false;


        public Assistant()
        {
            InitializeComponent();

            app_ftpserver.Text = App.config.AppSettings.Settings["app_ftpserver"].Value;
            app_username.Text = App.config.AppSettings.Settings["app_username"].Value;
            app_psw.Text = App.config.AppSettings.Settings["app_psw"].Value;
            app_locationUrl.Text = App.config.AppSettings.Settings["app_locationUrl"].Value;
        }


        //-------------- GENEL BUTONLAR -------------- //

        private void autoUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                if (ftpConnection)
                {
                    if (selectedId != -1)
                    {

                        // İzlemeye başlıyorum
                        watchStop.IsEnabled = true;
                        autoUpdateBtn.IsEnabled = false;
                        watcherStatus = true;
                        fileWatch(files[selectedId]);
                    }
                    else
                    {
                        MessageBox.Show("Dosya seçilmemiş");
                    }

                }
                else
                {
                    MessageBox.Show("Ftp bağlantısı yapılmamış");
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void ftpConnect_Click(object sender, RoutedEventArgs e)
        {

            try
            {

                // Location Url Hariç Tüm inputlar boş olamaz
                if (app_ftpserver.Text.Length > 0 && app_username.Text.Length > 0 && app_psw.Text.Length > 0)
                {

                    ftpConnect();

                    // İşlem başarılı olursa girilen giriş bilgilerini .config e kayıt ediyorum
                    App.config.AppSettings.Settings["app_ftpserver"].Value = app_ftpserver.Text;
                    App.config.AppSettings.Settings["app_username"].Value = app_username.Text;
                    App.config.AppSettings.Settings["app_psw"].Value = app_psw.Text;
                    App.config.AppSettings.Settings["app_locationUrl"].Value = app_locationUrl.Text;
                    App.config.Save(ConfigurationSaveMode.Modified);

                    ftpResponse.Text = "Ftp Successful";

                    // Tüm işlemler tamamsa artık ftp yazmaya hazırız.
                    ftpConnection = true;
                    autoUpdateBtn.IsEnabled = true;

                }
                else
                {
                    MessageBox.Show("Boş Alanları Doldurunuz");
                }
            }
            catch (Exception ex)
            {
                ftpResponse.Text = ex.Message;
            }



        }

        private void watchStop_Click(object sender, RoutedEventArgs e)
        {
            // watcherstatus false yaparak izleme döngüsünü bitiyor.
            watchStop.IsEnabled = false;
            watcherStatus = false;
        }

        private void resetButton_Click(object sender, RoutedEventArgs e)
        {
            totalChangeCountText.Text = "0";
            lastChangeText.Text = "Wait";
            resultText.Text = "Wait";
            selectedFileNewSizeText.Text = "";
            selectedFileNameText.Text = "";
            selectedFilePathText.Text = "";
            selectedFileSizeText.Text = "";
            selectedId = -1;
            leftStackPanel.IsEnabled = true;
            resetButton.IsEnabled = false;
        }


        //-------------- GENEL BUTONLAR -------------- //



        //-------------- FONKSİYONLAR -------------- //




        private async void ftpConnect()
        {
            try
            {
                // FTP Bağlantısı sağlıyorum
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(this.Dispatcher.Invoke(() => app_ftpserver.Text + app_locationUrl.Text));
                request.Method = WebRequestMethods.Ftp.ListDirectoryDetails;
                request.Credentials = new NetworkCredential(this.Dispatcher.Invoke(() => app_username.Text), this.Dispatcher.Invoke(() => app_psw.Text));


                // Bağlandığımız FTP sunucusundaki konumla ilgili dosyaları alıyorum



                using (FtpWebResponse response = (FtpWebResponse)await request.GetResponseAsync())
                {
                    using (Stream responseStream = response.GetResponseStream())
                    {
                        StreamReader reader = new StreamReader(responseStream);
                        // Yanıtı okuyarak dosya ve klasör listesini elde ediyorum
                        string directoryListing = reader.ReadToEnd();
                        string[] lines = directoryListing.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                        List<string> fileNames = new List<string>();
                        List<string> folderNames = new List<string>();

                        // Daha önceki bağlantıdan kalan verileri siliyorum
                        this.Dispatcher.Invoke(() => directoryListView.Items.Clear());

                        // Tipe göre dönerek yerleştiriyorum 
                        foreach (string line in lines)
                        {
                            string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            string fileType = parts[0];
                            string fileName = parts[parts.Length - 1];

                            // Dosya veya klasör olarak ayrıştırma
                            if (fileType.StartsWith("d"))
                            {
                                // Klasör
                                Debug.WriteLine("eKLEDİM");
                                this.Dispatcher.Invoke(() => directoryListView.Items.Add($"Folder: {fileName}"));
                            }
                            else
                            {
                                // Dosya
                                this.Dispatcher.Invoke(() => directoryListView.Items.Add($"File: {fileName}"));
                            }
                        }

                        this.Dispatcher.Invoke(() =>
                        {
                            if (app_locationUrl.Text.Length > 0) backFtpFolder.IsEnabled = true;
                            else backFtpFolder.IsEnabled = false;
                        });

                        ftpConnection = true;
                    }

                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }


        }

        private async void fileWatch(FileInfo file)
        {

            try
            {
                // Seçilen dosyayı izlemeye başlıyorum
                watcher = Task.Run(() =>
                {
                    FileInfo beforeFile = file;
                    FileInfo watchFile;
                    while (watcherStatus)
                    {
                        watchFile = new FileInfo(file.FullName);
                        if (watchFile.LastWriteTime != beforeFile.LastWriteTime || watchFile.Length != beforeFile.Length)
                        {
                            stopwatch.Start();
                            beforeFile = watchFile;
                            ftp_write(watchFile);

                        }
                        Thread.Sleep(1000);
                    }

                });

                await watcher;
                watcher.Dispose();
                autoUpdateBtn.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }



        }

        private async void ftp_download(string fileName, string path)
        {

            try
            {

                // İndirilecek dosyanın hedef yerel dosya yolu
                string localFilePath = path + "\\" + fileName;
                stopwatch.Restart();
                stopwatch.Start();

                // FTP bağlantısını oluşturma
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(new Uri(string.Format("{0}/{1}", this.Dispatcher.Invoke(() => app_ftpserver.Text + app_locationUrl.Text), fileName)));
                request.Method = WebRequestMethods.Ftp.DownloadFile;
                request.Credentials = new NetworkCredential(this.Dispatcher.Invoke(() => app_username.Text), this.Dispatcher.Invoke(() => app_psw.Text)); ;

                // FTP sunucudan dosyayı indirme
                using (FtpWebResponse response = (FtpWebResponse)await request.GetResponseAsync())
                {
                    using (Stream responseStream = response.GetResponseStream())
                    {
                        using (FileStream localFileStream = new FileStream(localFilePath, FileMode.Create))
                        {
                            byte[] buffer = new byte[1024 * 1024]; // 1 MB tampon
                            int bytesRead;
                            while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await localFileStream.WriteAsync(buffer, 0, bytesRead);
                            }
                        }
                    }
                    Debug.WriteLine("Dosya indirildi.");
                }

                stopwatch.Stop();
                this.Dispatcher.Invoke(() => processingTime.Text = string.Format("{0} {1}", stopwatch.Elapsed.TotalMilliseconds.ToString(), "ms"));
            }
            catch (WebException ex)
            {
                MessageBox.Show(ex.Message);
            }



        }

        private async void ftp_write(FileInfo file)
        {


            try
            {

                // Yazma işlemlerini yaptığım fonksiyon


                FtpWebRequest ftbrequest = (FtpWebRequest)WebRequest.Create(new Uri(string.Format("{0}/{1}", this.Dispatcher.Invoke(() => app_ftpserver.Text + app_locationUrl.Text), file.Name)));
                ftbrequest.Method = WebRequestMethods.Ftp.UploadFile;
                ftbrequest.Credentials = new NetworkCredential(this.Dispatcher.Invoke(() => app_username.Text), this.Dispatcher.Invoke(() => app_psw.Text));
                Stream ftbStream = ftbrequest.GetRequestStream();


                FileStream fs = File.OpenRead(file.FullName);
                byte[] buffer = new byte[4096];
                double total = (double)fs.Length;
                double read = 0;
                int byteRead = 0;

                do
                {
                    byteRead = fs.Read(buffer, 0, 1024);
                    ftbStream.Write(buffer, 0, byteRead);
                    read += (double)byteRead;
                    double prec = read / total * 100;
                    if (total == 0) prec = 100;


                }
                while (byteRead != 0);

                this.Dispatcher.Invoke(() =>
                {
                    int totalCount = int.Parse(totalChangeCountText.Text);
                    totalCount++;
                    totalChangeCountText.Text = totalCount.ToString();
                    lastChangeText.Text = DateTime.Now.ToString();
                    resultText.Text = "Changed successfully";
                    selectedFileNewSizeText.Text = file.Length.ToString() + " byte";
                    processingTime.Text = string.Format("{0} {1}", stopwatch.Elapsed.TotalMilliseconds.ToString(), "ms");
                    stopwatch.Restart();

                });


                fs.Close();
                ftbStream.Close();
                stopwatch.Stop();
            }
            catch
            {

                this.Dispatcher.Invoke(() =>
                {

                    resultText.Text = "Fail";

                });

            }


        }

        private async void ftp_delete(string fileName)
        {

            try
            {
                // Listviewde secilen dosyayı ftp sunucusundan siliyorum

                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(new Uri(string.Format("{0}/{1}", this.Dispatcher.Invoke(() => app_ftpserver.Text + app_locationUrl.Text), fileName)));
                request.Method = WebRequestMethods.Ftp.DeleteFile;
                request.Credentials = new NetworkCredential(this.Dispatcher.Invoke(() => app_username.Text), this.Dispatcher.Invoke(() => app_psw.Text)); ;
                FtpWebResponse response = (FtpWebResponse)request.GetResponse();
                stopwatch.Stop();
                this.Dispatcher.Invoke(() => processingTime.Text = string.Format("{0} {1}", stopwatch.Elapsed.TotalMilliseconds.ToString(), "ms"));
                ftpConnect();
                MessageBox.Show($"{fileName} adlı dosya başarılı bir sekilde silindi.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }


        }




        //-------------- FONKSİYONLAR -------------- //


        //-------------- CLICK FONKSİYONLAR -------------- //

        private void fileOpen_Click(object sender, RoutedEventArgs e)
        {

            try
            {
                // Dosya yükleme işemleri yapıyorum

                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "Text tabanlı dosyalar|*.cs;*.js;*.py;*.cpp;*.java;*.php;*.html;*.css;*.jsx:*.config;*.json;*.txt";


                if (openFileDialog.ShowDialog() == true)
                {

                    // Dosya secilmiş ise bir dinamik button oluşturup ekliyorum.
                    FileInfo file = new FileInfo(openFileDialog.FileName);
                    files.Add(file);

                    var btntemp = new Button();
                    btntemp.Click += fileSelected_Click;
                    btntemp.Style = (Style)FindResource("fileButtonDefult");

                    btntemp.Content = file.Name;
                    btntemp.Uid = files.Count.ToString();
                    int totalFileCount = int.Parse(totalfile.Text);
                    totalfile.Text = (++totalFileCount).ToString();
                    fileStack.Children.Add(btntemp);


                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }


        }

        private void openFolderFtp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                openFolderFtp.IsEnabled = false;
                object selectedItem = directoryListView.SelectedItem;
                app_locationUrl.Text += selectedItem?.ToString().Split(": ")[1].ToString() + "/";
                ftpConnect();
            }
            catch
            {

            }

        }

        private void backFtpFolder_Click(object sender, RoutedEventArgs e)
        {

            try
            {
                string[] lastLocation = app_locationUrl.Text.Split('/');
                int totalC = lastLocation.Length;
                totalC -= 2;
                app_locationUrl.Text = app_locationUrl.Text.Replace(lastLocation[totalC] + '/', "");
                ftpConnect();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }


        }

        private void downloadFtp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // listviewde seçilen dosyayı seçtiğim klasöre indiriyorum

                System.Windows.Forms.FolderBrowserDialog folderDialog = new System.Windows.Forms.FolderBrowserDialog();
                var result = folderDialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    string selectedFolder = folderDialog.SelectedPath;
                    object selectedItem = this.Dispatcher.Invoke(() => directoryListView.SelectedItem);
                    ftp_download(selectedItem?.ToString().Split(": ")[1].ToString(), selectedFolder);

                }



            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }


        }

        private async void deleteFtp_Click(object sender, RoutedEventArgs e)
        {

            try
            {
                // listview de secilen dosyayı siliyorum
                stopwatch.Start();
                downloadFtp.IsEnabled = false;
                deleteFtp.IsEnabled = false;
                other = Task.Run(() =>
                {

                    object selectedItem = this.Dispatcher.Invoke(() => directoryListView.SelectedItem);
                    ftp_delete(selectedItem?.ToString().Split(": ")[1].ToString());

                });
                await other;
                other.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

        }

        private void fileSelected_Click(object sender, RoutedEventArgs e)
        {

            try
            {
                // Dosya secme işlemleri 
                var selected = sender as Button;
                selectedId = int.Parse(selected.Uid);
                selectedId--;
                selectedFileNameText.Text = files[selectedId].Name;
                selectedFilePathText.Text = files[selectedId].FullName;
                selectedFileSizeText.Text = files[selectedId].Length.ToString() + " byte";


                leftStackPanel.IsEnabled = false;
                ftpConnectBtn.IsEnabled = true;
                resetButton.IsEnabled = true;
                totalChangeCountText.Text = totalChange.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

        }

        private void directoryListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                object selectedItem = directoryListView.SelectedItem;

                // Seçili öğeyi MessageBox ile göster
                if (selectedItem?.ToString().Split(':')[0].ToString() == "Folder")
                {

                    app_locationUrl.Text += selectedItem?.ToString().Split(": ")[1].ToString() + "/";
                    ftpConnect();
                }
                else
                {
                    MessageBox.Show("Dosya secildi...");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

        }



        //-------------- CLICK FONKSİYONLAR -------------- //



        //-------------- CHANGE FONKSİYONLAR -------------- //

        private void directoryListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // Listview de değişiklikleri kontrol ediyorum.
                object selectedItem = directoryListView.SelectedItem;
                string type = selectedItem?.ToString().Split(':')[0].ToString();

                switch (type)
                {

                    case "Folder":
                        downloadFtp.IsEnabled = false;
                        deleteFtp.IsEnabled = false;
                        openFolderFtp.IsEnabled = true;
                        break;
                    case "File":
                        downloadFtp.IsEnabled = true;
                        deleteFtp.IsEnabled = true;
                        openFolderFtp.IsEnabled = false;
                        break;
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }


        }


        //-------------- CHANGE FONKSİYONLAR -------------- //
    }
}
