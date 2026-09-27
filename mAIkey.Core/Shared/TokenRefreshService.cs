using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace mAIkey.Core.Services
{
    /// <summary>
    /// Service for automatic token refresh management
    /// Runs background checks and refreshes access tokens before they expire
    /// </summary>
    public class TokenRefreshService : IDisposable
    {
        private readonly ApiClient _apiClient;
        private System.Timers.Timer? _refreshTimer;
        private string? _refreshToken;
        private DateTime _tokenExpiresAt;
        private readonly LoggingService _loggingService;
        private bool _isRefreshing = false;

        public event EventHandler<string>? TokenRefreshed;
        public event EventHandler? TokenExpired;

        public TokenRefreshService(ApiClient apiClient)
        {
            _apiClient = apiClient;
            _loggingService = new LoggingService();
        }

        /// <summary>
        /// Initialize the token refresh service with existing tokens
        /// Note: No background timer - refresh happens only on app startup
        /// </summary>
        public void Initialize(string accessToken, string refreshToken, int expiresInSeconds = 2592000) // 30 days default
        {
            _refreshToken = refreshToken;
            _tokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresInSeconds);

            _ = _loggingService.LogInfoAsync("TokenRefresh", $"TokenRefreshService initialized. Token expires at: {_tokenExpiresAt}");

            // No background timer for battery/resource efficiency
            // Token refresh happens at app startup instead
        }

        /// <summary>
        /// REMOVED: Background timer for battery/resource efficiency
        /// Token refresh now happens only at app startup (on-demand)
        /// </summary>
        [Obsolete("Background timer removed - refresh happens at app startup instead")]
        private void StartRefreshTimer()
        {
            // No longer used - kept for backwards compatibility
        }

        /// <summary>
        /// Check if token is close to expiry and refresh if needed
        /// Now checks for <5 days remaining (was <10 minutes)
        /// </summary>
        public async Task CheckAndRefreshTokenAsync()
        {
            if (_isRefreshing)
            {
                _ = _loggingService.LogInfoAsync("TokenRefresh", "Token refresh already in progress, skipping...");
                return;
            }

            if (string.IsNullOrEmpty(_refreshToken))
            {
                _ = _loggingService.LogInfoAsync("TokenRefresh", "No refresh token available");
                return;
            }

            // Check time until expiry
            var timeUntilExpiry = _tokenExpiresAt - DateTime.UtcNow;

            _ = _loggingService.LogInfoAsync("TokenRefresh", $"Token check: Expires in {timeUntilExpiry.TotalDays:F1} days");

            // Refresh if less than 5 days remaining (buffer for token rotation)
            if (timeUntilExpiry.TotalDays < 5)
            {
                _ = _loggingService.LogInfoAsync("TokenRefresh", "Token expires within 5 days, refreshing...");
                await RefreshAccessTokenAsync();
            }
        }

        /// <summary>
        /// Manually refresh the access token using the refresh token
        /// </summary>
        public async Task<bool> RefreshAccessTokenAsync()
        {
            if (_isRefreshing)
            {
                _ = _loggingService.LogInfoAsync("TokenRefresh", "Refresh already in progress");
                return false;
            }

            if (string.IsNullOrEmpty(_refreshToken))
            {
                _ = _loggingService.LogWarningAsync("TokenRefresh", "Cannot refresh: no refresh token available");
                TokenExpired?.Invoke(this, EventArgs.Empty);
                return false;
            }

            _isRefreshing = true;

            try
            {
                _ = _loggingService.LogInfoAsync("TokenRefresh", "Calling /auth/refresh endpoint...");

                var response = await _apiClient.RefreshTokenAsync(_refreshToken);

                if (response != null && !string.IsNullOrEmpty(response.Token))
                {
                    // Update stored tokens
                    _apiClient.SetAuthToken(response.Token);

                    if (!string.IsNullOrEmpty(response.RefreshToken))
                    {
                        _refreshToken = response.RefreshToken;
                    }

                    // Update expiry time (30 days from now)
                    _tokenExpiresAt = DateTime.UtcNow.AddDays(30);

                    _ = _loggingService.LogInfoAsync("TokenRefresh", $"✅ Token refreshed successfully. New expiry: {_tokenExpiresAt}");

                    // Notify listeners (e.g., to update stored credentials)
                    TokenRefreshed?.Invoke(this, response.Token);

                    return true;
                }
                else
                {
                    _ = _loggingService.LogWarningAsync("TokenRefresh", "Token refresh failed: empty response");
                    TokenExpired?.Invoke(this, EventArgs.Empty);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _ = _loggingService.LogExceptionAsync("TokenRefresh", ex);

                // If refresh token is expired, notify user
                if (ex.Message.Contains("REFRESH_TOKEN_EXPIRED") || ex.Message.Contains("403"))
                {
                    _ = _loggingService.LogWarningAsync("TokenRefresh", "Refresh token expired - user needs to re-login");
                    TokenExpired?.Invoke(this, EventArgs.Empty);
                }

                return false;
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        /// <summary>
        /// Check if current tokens are valid and refresh if needed
        /// Called on app startup - this is the PRIMARY refresh trigger
        /// </summary>
        public async Task<bool> ValidateAndRefreshOnStartupAsync()
        {
            if (string.IsNullOrEmpty(_refreshToken))
            {
                _ = _loggingService.LogInfoAsync("TokenRefresh", "Startup check: No refresh token available");
                return false;
            }

            var timeUntilExpiry = _tokenExpiresAt - DateTime.UtcNow;

            _ = _loggingService.LogInfoAsync("TokenRefresh", $"Startup token check: Expires in {timeUntilExpiry.TotalDays:F1} days");

            // If token already expired or expires in <5 days, refresh proactively
            if (timeUntilExpiry.TotalSeconds < 0 || timeUntilExpiry.TotalDays < 5)
            {
                _ = _loggingService.LogInfoAsync("TokenRefresh", "Token expired or expires within 5 days, refreshing...");
                return await RefreshAccessTokenAsync();
            }

            _ = _loggingService.LogInfoAsync("TokenRefresh", "Token still valid, no refresh needed");
            return true;
        }

        /// <summary>
        /// Update the refresh token (e.g., after new login)
        /// </summary>
        public void UpdateRefreshToken(string refreshToken, int expiresInSeconds = 2592000) // 30 days default
        {
            _refreshToken = refreshToken;
            _tokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresInSeconds);
            _ = _loggingService.LogInfoAsync("TokenRefresh", $"Refresh token updated. New expiry: {_tokenExpiresAt}");
        }

        /// <summary>
        /// Stop the refresh timer and clear tokens
        /// </summary>
        public void Stop()
        {
            _refreshTimer?.Stop();
            _refreshTimer?.Dispose();
            _refreshToken = null;
            _ = _loggingService.LogInfoAsync("TokenRefresh", "Token refresh service stopped");
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
