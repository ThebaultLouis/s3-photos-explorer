using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.CognitoIdentity;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;


namespace S3PhotosExplorerWindowsDesktop
{
    public sealed partial class MainWindow : Window
    {
        const string bucketName = "BUCKET_NAME";
        const string identityPoolId = "IDENTITY_POOL_ID";
        static readonly RegionEndpoint bucketRegion = RegionEndpoint.USEast1;

        IAmazonS3 s3Client;

        public MainWindow()
        {
            InitializeComponent();
            var credentials = new CognitoAWSCredentials(identityPoolId, bucketRegion);
            s3Client = new AmazonS3Client(credentials, bucketRegion);

        }

        private async void ListAlbums_Click(object? sender, RoutedEventArgs? e)
        {
            AlbumListView.Items.Clear();

            var request = new ListObjectsV2Request
            {
                BucketName = bucketName,
                Delimiter = "/"
            };

            var response = await s3Client.ListObjectsV2Async(request);

            foreach (var prefix in response.CommonPrefixes)
            {
                string albumName = Uri.UnescapeDataString(prefix.TrimEnd('/'));
                var item = new ListViewItem { Content = albumName };
                item.Tapped += (s, args) => ViewAlbum(albumName);
                AlbumListView.Items.Add(item);
            }
        }

        private async void CreateAlbum_Click(object sender, RoutedEventArgs e)
        {
            var inputDialog = new TextBox { PlaceholderText = "Enter album name..." };
            var dialog = new ContentDialog
            {
                Title = "Create Album",
                Content = inputDialog,
                PrimaryButtonText = "Create",
                CloseButtonText = "Cancel"
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var albumName = inputDialog.Text.Trim();
                if (string.IsNullOrWhiteSpace(albumName) || albumName.Contains("/"))
                {
                    ShowMessage("Invalid album name.");
                    return;
                }

                var albumKey = Uri.EscapeDataString(albumName) + "/";
                var request = new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = albumKey,
                    ContentBody = string.Empty
                };

                await s3Client.PutObjectAsync(request);
                ShowMessage("Album created.");
                ListAlbums_Click(null, null);
            }
        }

        private async void ViewAlbum(string albumName)
        {
            var dialog = new ContentDialog
            {
                Title = $"Album: {albumName}",
                CloseButtonText = "Close"
            };

            var panel = new StackPanel();

            var albumPrefix = Uri.EscapeDataString(albumName) + "/";
            var listRequest = new ListObjectsV2Request
            {
                BucketName = bucketName,
                Prefix = albumPrefix
            };

            var listResponse = await s3Client.ListObjectsV2Async(listRequest);
            foreach (var photo in listResponse.S3Objects)
            {
                var photoUrl = $"https://{bucketName}.s3.amazonaws.com/{Uri.EscapeDataString(photo.Key)}";
                var image = new Image { Width = 128, Height = 128, Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(photoUrl)) };
                panel.Children.Add(image);
            }

            var uploadButton = new Button { Content = "Add Photo" };
            uploadButton.Click += async (s, e) => await AddPhoto(albumName);

            panel.Children.Add(uploadButton);
            dialog.Content = panel;
            await dialog.ShowAsync();
        }

        private async Task AddPhoto(string albumName)
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".png");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                using var stream = await file.OpenStreamForReadAsync();
                var albumPrefix = Uri.EscapeDataString(albumName) + "/";
                var uploadRequest = new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = albumPrefix + file.Name,
                    InputStream = stream
                };
                await s3Client.PutObjectAsync(uploadRequest);
                ShowMessage("Photo uploaded.");
            }
        }

        private async void ShowMessage(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "Message",
                Content = message,
                CloseButtonText = "OK"
            };
            await dialog.ShowAsync();
        }
    }
}
