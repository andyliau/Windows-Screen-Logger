using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace WindowsActivityLogger.Tests
{
    public class BitmapMemoryTests
    {
        [Fact(Skip = "Requires a running WinForms message pump (STA + UI thread). Run manually or in a dedicated STA test host.")]
        public void CaptureAllScreens_ShouldNotLeakMemory()
        {
            // MainForm requires STA apartment state (WinForms requirement).
            // xunit 2.9+ runs async tests on MTA thread pool threads, so we wrap explicitly.
            Exception? threadException = null;
            var thread = new Thread(() =>
            {
                try { RunAsync().GetAwaiter().GetResult(); }
                catch (Exception ex) { threadException = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (threadException is not null)
                ExceptionDispatchInfo.Capture(threadException).Throw();
        }

        private static async Task RunAsync()
        {
            var differenceAt1 = await RunCaptureMultipleTimes(1);
            var differenceAt20 = await RunCaptureMultipleTimes(10);

            Assert.True(differenceAt20.afterCollection < differenceAt1.afterCollection * 2 + 200,
                $@"Memory leak detected: Memory usage after garbage collection increased significantly after multiple captures.
differenceAt1: {differenceAt1.afterCollection}
differenceAt20: {differenceAt20.afterCollection}");

            Assert.True(differenceAt20.beforeCollection < differenceAt1.beforeCollection * 2 + 200,
                @$"Memory leak detected: Memory usage before garbage collection increased significantly after multiple captures.
differenceAt1: {differenceAt1.beforeCollection}
differenceAt20: {differenceAt20.beforeCollection}");
        }

		private static async Task<(long beforeCollection, long afterCollection)> RunCaptureMultipleTimes(int runTimes)
		{
			using (var form = new MainForm())
			{
				var captureMethod = typeof(MainForm).GetMethod("CaptureAllScreensAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

				var initialMemory = GC.GetTotalMemory(true);
				for (int i = 0; i < runTimes; i++)
				{
					var task = (Task)captureMethod!.Invoke(form, null)!;
					await task;
				}

				var adjustment = runTimes * 200; // Adjust this value based on expected memory usage per capture
				var differenceBeforeCollection = GC.GetTotalMemory(false) - initialMemory - adjustment;
				differenceBeforeCollection = Math.Max(differenceBeforeCollection, 0); // Ensure no negative memory difference
				GC.Collect();
				GC.WaitForPendingFinalizers();
				GC.Collect(); // Collect again to ensure all finalizers are run
				var differenceAfterCollection = GC.GetTotalMemory(true) - initialMemory - adjustment;
				differenceAfterCollection = Math.Max(differenceAfterCollection, 0); // Ensure no negative memory difference
				return (differenceBeforeCollection, differenceAfterCollection);
			}
		}
	}
}
