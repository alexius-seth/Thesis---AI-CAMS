namespace ACPAMS.Services
{
    /// <summary>
    /// Runs continuously in the background (started once from App.xaml.cs),
    /// polling the AiJob queue and draining it every tick. Every instance of
    /// the app (each student PC, the instructor's PC) runs its own copy of
    /// this — that's fine, since AiJobService.ProcessNextQueuedJobAsync claims
    /// a job (Status → "processing") before doing any slow work, so multiple
    /// instances polling at once mostly just means the queue gets drained
    /// faster, not duplicated work (see the race-condition caveat in
    /// AiJobService for the one edge case where two instances could grab the
    /// same job in the same instant — acceptable for a lab-scale deployment).
    /// </summary>
    public class AiJobProcessorService
    {
        private readonly System.Timers.Timer _timer;
        private readonly AiJobService _jobService = new();
        private bool _isProcessing;

        public AiJobProcessorService(TimeSpan interval)
        {
            _timer = new System.Timers.Timer(interval.TotalMilliseconds);
            _timer.Elapsed += async (s, e) => await TickAsync();
            _timer.AutoReset = true;
        }

        public void Start() => _timer.Start();
        public void Stop() => _timer.Stop();

        private async Task TickAsync()
        {
            // Don't start a new tick's work if the previous tick is still
            // running (e.g. a slow Gemini call took longer than the interval).
            if (_isProcessing) return;
            _isProcessing = true;

            try
            {
                // Drain the whole queue each tick, not just one job at a time,
                // so a backlog doesn't linger across multiple intervals.
                bool didWork;
                do
                {
                    didWork = await _jobService.ProcessNextQueuedJobAsync();
                } while (didWork);
            }
            catch
            {
                // Swallow at this level — individual job failures are already
                // recorded on the AiJob row itself (Status="failed", LastError).
                // A poller-level exception here must never crash the whole app.
            }
            finally
            {
                _isProcessing = false;
            }
        }
    }
}
