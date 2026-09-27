using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace mAIkey.Core.Services
{
    /// <summary>
    /// Voert een OAuth "social login" flow uit (Google, Microsoft, …): start een sessie
    /// op de backend, opent de authorization-URL in de standaardbrowser, en pollt tot de
    /// gebruiker klaar is (of tot annulering/timeout). Provider-onafhankelijk via delegates.
    /// </summary>
    public static class OAuthLoginHelper
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

        /// <summary>
        /// <paramref name="start"/> start de flow (retourneert url + sessionId),
        /// <paramref name="poll"/> pollt één keer met dat sessionId.
        /// Retourneert Success=true met tokens/user bij succes, anders Success=false met
        /// Error/ErrorType (bv. TIMEOUT, BROWSER_FAILED, CANCELLED).
        /// </summary>
        public static async Task<LoginResponse> RunAsync(
            Func<Task<OAuthStartResponse>> start,
            Func<string, Task<LoginResponse>> poll,
            CancellationToken ct)
        {
            // 1) Start sessie -> URL + sessionId
            var s = await start();
            if (!s.Success || string.IsNullOrEmpty(s.Url) || string.IsNullOrEmpty(s.SessionId))
            {
                return new LoginResponse
                {
                    Success = false,
                    Error = s.Error,
                    ErrorType = s.ErrorType ?? "OAUTH_START_FAILED"
                };
            }

            // 2) Open browser
            try
            {
                Process.Start(new ProcessStartInfo(s.Url!) { UseShellExecute = true });
            }
            catch
            {
                return new LoginResponse { Success = false, ErrorType = "BROWSER_FAILED" };
            }

            // 3) Poll tot ready / error / timeout / annulering
            var deadline = DateTime.UtcNow.Add(Timeout);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    await Task.Delay(PollInterval, ct);
                }
                catch (OperationCanceledException)
                {
                    return new LoginResponse { Success = false, ErrorType = "CANCELLED" };
                }

                var poll1 = await poll(s.SessionId!);

                if (poll1.Success)
                    return poll1; // bevat token, refreshToken, user

                if (string.Equals(poll1.Status, "pending", StringComparison.OrdinalIgnoreCase))
                    continue;

                return poll1; // definitieve fout
            }

            return new LoginResponse { Success = false, ErrorType = "TIMEOUT" };
        }
    }
}
