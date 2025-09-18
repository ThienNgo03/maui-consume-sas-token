using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Maui.Controls;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AzureTestProject
{
    public partial class MainPage : ContentPage
    {

        // Your SAS URI and userId (store securely in production)
        //http://127.0.0.1:10000/devstoreaccount1/container1?sv=2024-05-04&se=2025-09-19T16%3A15%3A12Z&sr=c&sp=racwdxltfi&sig=igVCpZZykv%2Bj1aojiiQzMT3wXa0jBYPyJzgZ9bd0biY%3D
        private readonly string downloadFileName = "example.png"; // Example userId to match metadata

        public MainPage()
        {
            InitializeComponent();
            var localFilePath = Path.Combine(FileSystem.CacheDirectory, downloadFileName);

            if (File.Exists(localFilePath))
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    FileImage.Source = ImageSource.FromFile(localFilePath);
                });
            }
        }

        #region [UsingDisk]
        private async void DownloadToDiskAndDisplayButton(object sender, EventArgs e)
        {
            try
            {
                var localFilePath = Path.Combine(FileSystem.CacheDirectory, downloadFileName);

                if (File.Exists(localFilePath))
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        FileImage.Source = ImageSource.FromFile(localFilePath);
                    });
                }
                else
                {
                    bool success = await DownloadImageFromBlobToDiskAsync(localFilePath);

                    if (success) // if này để dùng cho trường hợp muốn lưu file rồi hiển thị ảnh từ file đã lưu
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            FileImage.Source = ImageSource.FromFile(localFilePath);
                        });
                    }

                    else
                    {
                        await DisplayAlert("Error", "Could not find image with matching metadata.", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Download Error", ex.Message, "OK");
            }
        }
        private async Task<bool> DownloadImageFromBlobToDiskAsync(string localFilePath)
        {
            var containerClient = new BlobContainerClient(new Uri(SasTokenEntry.Text));
            BlobClient? targetBlobClient = null;

            if (!FindByUserId.IsToggled)
            {
                targetBlobClient = containerClient.GetBlobClient(UserIdOrBlobName.Text);
            }
            else
            {
                await foreach (BlobItem blobItem in containerClient.GetBlobsAsync(BlobTraits.Metadata))
                {
                    if (blobItem.Metadata.TryGetValue("userId", out var value) && value == UserIdOrBlobName.Text)
                    {
                        targetBlobClient = containerClient.GetBlobClient(blobItem.Name);
                        break;
                    }
                }
            }

            if (targetBlobClient == null)
                return false;

            BlobDownloadInfo download = await targetBlobClient.DownloadAsync();

            // Save to local file
            using (var fileStream = File.Open(localFilePath, FileMode.Create, FileAccess.Write))
            {
                await download.Content.CopyToAsync(fileStream);
            }

            return true;
        }

        private void DeleteCachedImage(object? sender, EventArgs e)
        {
            var fileName = UserIdOrBlobName.Text;
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        private void DeleteAllCachedImages(object? sender, EventArgs e)
        {
            var cacheDir = FileSystem.CacheDirectory;
            var files = Directory.GetFiles(cacheDir);

            foreach (var file in files)
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    // Nếu cần, bạn có thể log lỗi hoặc hiển thị thông báo
                    Console.WriteLine($"Không thể xóa file: {file}. Lỗi: {ex.Message}");
                }
            }
        }

        private async void ReloadCacheImageAsync(object sender, EventArgs e)
        {
            try
            {
                var localFilePath = Path.Combine(FileSystem.CacheDirectory, downloadFileName);
                if (File.Exists(localFilePath))
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        FileImage.Source = ImageSource.FromFile(localFilePath);
                    });
                }
                else
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        FileImage.Source = null; // hoặc dùng ảnh mặc định nếu muốn
                    });
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Reload Error", ex.Message, "OK");
            }
        }

        #endregion

        #region [UsingRAM]
        private async void DownloadToRamAndDisplayButton(object sender, EventArgs e)
        {
            try
            {

                // khởi tạo memory stream để hiển thị ảnh từ memory stream
                var ms = new MemoryStream();
                bool success = await DownloadImageFromBlobToRamAsync(ms);
                if (success)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        StreamImage.Source = ImageSource.FromStream(() => ms);
                    });
                }
                else
                {
                    await DisplayAlert("Error", "Could not find image with matching metadata.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Download Error", ex.Message, "OK");
            }
        }
        private async Task<bool> DownloadImageFromBlobToRamAsync(MemoryStream ms)
        {
            var containerClient = new BlobContainerClient(new Uri(SasTokenEntry.Text));
            BlobClient? targetBlobClient = null;
            if (!FindByUserId.IsToggled)
            {
                targetBlobClient = containerClient.GetBlobClient(UserIdOrBlobName.Text);
            }
            else
            {
                await foreach (BlobItem blobItem in containerClient.GetBlobsAsync(BlobTraits.Metadata))
                {
                    if (blobItem.Metadata.TryGetValue("userId", out var value) && value == UserIdOrBlobName.Text)
                    {
                        targetBlobClient = containerClient.GetBlobClient(blobItem.Name);
                        break;
                    }
                }
            }
            if (targetBlobClient == null)
                return false;

            BlobDownloadInfo download = await targetBlobClient.DownloadAsync();

            // Load into memory stream
            await download.Content.CopyToAsync(ms);
            ms.Position = 0;

            return true;
        }

        #endregion

        private async void DisplayImageByUrlButton(object sender, EventArgs e)
        {
            try
            {

                // khởi tạo memory stream để hiển thị ảnh từ memory stream
                string blobUrl = await CreateImageUrl();
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    UrlImage.Source = ImageSource.FromUri(new Uri(blobUrl));
                });
            }
            catch (Exception ex)
            {
                await DisplayAlert("Download Error", ex.Message, "OK");
            }
        }

        private async Task<string> CreateImageUrl()
        {
            string blobName = UserIdOrBlobName.Text;
            var containerClient = new BlobContainerClient(new Uri(SasTokenEntry.Text));
            if (FindByUserId.IsToggled)
            {
                await foreach (BlobItem blobItem in containerClient.GetBlobsAsync(BlobTraits.Metadata))
                {
                    if (blobItem.Metadata.TryGetValue("userId", out var value) && value == UserIdOrBlobName.Text)
                    {
                        blobName = blobItem.Name;
                        break;
                    }
                }
            }
            // Tách phần token ra khỏi baseUri
            var uriParts = SasTokenEntry.Text.Split('?');
            string containerUri = uriParts[0];
            string sasToken = uriParts.Length > 1 ? uriParts[1] : "";

            // Tạo URL đầy đủ đến blob
            return $"{containerUri}/{blobName}?{sasToken}";

        }
    }
}
