using System;
using Xunit;
using WindowsScreenLogger;

namespace WindowsScreenLogger.Tests
{
    public class BitmapMemoryTests
    {
        [Fact]
        public void CaptureAllScreens_ShouldNotLeakMemory()
        {
            long initialMemory = GC.GetTotalMemory(true);

            for (int i = 0; i < 10; i++)
            {
                using (var form = new MainForm())
                {
                    // Ensure screen capture is initialized
                    var method = typeof(MainForm).GetMethod("InitializeScreenCapture", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    method.Invoke(form, null);

                    // Call the production capture method
                    var captureMethod = typeof(MainForm).GetMethod("CaptureAllScreens", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    captureMethod.Invoke(form, null);
                }
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            long finalMemory = GC.GetTotalMemory(true);

            // Allow some fluctuation, but memory should not grow unbounded
            Assert.True(finalMemory - initialMemory < 10 * 1024 * 1024); // <10MB difference
        }
    }
}
