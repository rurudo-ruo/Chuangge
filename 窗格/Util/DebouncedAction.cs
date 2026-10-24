using System;
using System.Windows.Forms;

namespace 窗格.Util
{
    internal sealed class DebouncedAction : IDisposable
    {
        private readonly Timer timer;
        private readonly Action<Exception> errorHandler;
        private Action pendingAction;
        private bool disposed;

        public DebouncedAction(TimeSpan delay, Action<Exception> errorHandler)
        {
            timer = new Timer
            {
                Interval = Math.Max(1, (int)Math.Ceiling(delay.TotalMilliseconds))
            };
            this.errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
            timer.Tick += Timer_Tick;
        }

        public void Schedule(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            ThrowIfDisposed();

            pendingAction = action;
            timer.Stop();
            timer.Start();
        }

        public void Cancel()
        {
            ThrowIfDisposed();
            timer.Stop();
            pendingAction = null;
        }

        public void Flush()
        {
            ThrowIfDisposed();
            timer.Stop();
            var action = pendingAction;
            if (action == null)
                return;

            pendingAction = null;
            try
            {
                action();
            }
            catch
            {
                pendingAction = action;
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            timer.Stop();
            timer.Tick -= Timer_Tick;
            timer.Dispose();
            pendingAction = null;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                Flush();
            }
            catch (Exception ex)
            {
                errorHandler(ex);
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(DebouncedAction));
        }
    }
}
