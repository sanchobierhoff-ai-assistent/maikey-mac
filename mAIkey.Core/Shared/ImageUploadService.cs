using System;
using System.Threading.Tasks;

namespace mAIkey.Core.Services
{
    /// <summary>
    /// Service voor het uploaden van afbeeldingen via de backend naar Cloudinary.
    /// Cloudinary-credentials staan alleen op de server — nooit in de client.
    /// </summary>
    public class ImageUploadService
    {
        private readonly ApiClient _apiClient;
        private DateTime _lastUploadTime = DateTime.MinValue;
        private const int MinUploadIntervalMs = 500;

        public ImageUploadService(ApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        /// <summary>
        /// Upload een base64 image via de backend en krijg publieke URL terug.
        /// </summary>
        public async Task<CloudinaryImageResponse?> UploadImageAsync(string base64Image)
        {
            try
            {
                // Rate limiting
                if (_lastUploadTime != DateTime.MinValue)
                {
                    var elapsed = DateTime.Now - _lastUploadTime;
                    if (elapsed.TotalMilliseconds < MinUploadIntervalMs)
                    {
                        int delayMs = MinUploadIntervalMs - (int)elapsed.TotalMilliseconds;
                        Logger.Log($"⏱️ Rate limiting: waiting {delayMs}ms");
                        await Task.Delay(delayMs);
                    }
                }
                _lastUploadTime = DateTime.Now;

                Logger.Log("📤 Uploading image via backend...");

                var result = await _apiClient.UploadImageAsync(base64Image);

                if (result?.Success == true && !string.IsNullOrEmpty(result.ImageUrl))
                {
                    Logger.Log($"✅ Image uploaded successfully");
                    Logger.Log($"   URL: {result.ImageUrl}");
                    Logger.Log($"   Public ID: {result.PublicId}");

                    return new CloudinaryImageResponse
                    {
                        Success = true,
                        ImageUrl = result.ImageUrl,
                        PublicId = result.PublicId,
                        Timestamp = DateTime.Now
                    };
                }
                else
                {
                    Logger.Log($"❌ Upload failed: {result?.Error}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"❌ Error uploading image: {ex.Message}");
                Logger.Log($"   Type: {ex.GetType().Name}");
                if (ex.InnerException != null)
                    Logger.Log($"   Inner: {ex.InnerException.Message}");
                return null;
            }
        }

    }

    /// <summary>
    /// Response model voor image upload.
    /// </summary>
    public class CloudinaryImageResponse
    {
        public bool Success { get; set; }
        public string ImageUrl { get; set; } = "";
        public string PublicId { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }
}
