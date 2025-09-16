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
        private readonly string userId = "12346";
        private readonly string downloadFileName = "downloaded_by_metadata.png";

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

        private async Task<MemoryStream> DisplayImageFromBlobAsync(MemoryStream ms)
        {
            var containerClient = new BlobContainerClient(new Uri(SasTokenEntry.Text));
            BlobClient? targetBlobClient = null;

            await foreach (BlobItem blobItem in containerClient.GetBlobsAsync(BlobTraits.Metadata))
            {
                if (blobItem.Metadata.TryGetValue("userId", out var value) && value == userId)
                {
                    targetBlobClient = containerClient.GetBlobClient(blobItem.Name);
                    break;
                }
            }

            if (targetBlobClient == null)
                return ms;

            BlobDownloadInfo download = await targetBlobClient.DownloadAsync();

            // Load into memory stream
            await download.Content.CopyToAsync(ms);
            ms.Position = 0;

            return ms;
        }

        private async Task<bool> DownloadImageFromBlobAsync(string localFilePath)
        {
            var containerClient = new BlobContainerClient(new Uri(SasTokenEntry.Text));
            BlobClient? targetBlobClient = null;

            await foreach (BlobItem blobItem in containerClient.GetBlobsAsync(BlobTraits.Metadata))
            {
                if (blobItem.Metadata.TryGetValue("userId", out var value) && value == userId)
                {
                    targetBlobClient = containerClient.GetBlobClient(blobItem.Name);
                    break;
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
            var fileName = "downloaded_by_metadata.png";
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        private async void FileDownloadButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                // khởi tạo đường dẫn lưu file tạm thời
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
                    bool success = await DownloadImageFromBlobAsync(localFilePath);

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

        private async void DownloadStreamImage_Clicked(object sender, EventArgs e)
        {
            try
            {

                // khởi tạo memory stream để hiển thị ảnh từ memory stream
                var ms = new MemoryStream();
                ms = await DisplayImageFromBlobAsync(ms);
                if (ms.Length != 0) // if này để dùng cho trường hợp không muốn lưu file mà chỉ hiển thị ảnh từ memory stream
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
    }
}
