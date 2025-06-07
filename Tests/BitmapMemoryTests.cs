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
			var differenceAt1 = RunCaptureMultipleTimes(1);
			var differenceAt20 = RunCaptureMultipleTimes(20);

			// After garbage collection, memory should be same on both
			Assert.True(differenceAt20.afterCollection < differenceAt1.afterCollection * 2, 
				$@"Memory leak detected: Memory usage after garbage collection increased significantly after multiple captures.
differenceAt1: {differenceAt1.afterCollection}
differenceAt20: {differenceAt20.afterCollection}");

			Assert.True(differenceAt20.beforeCollection < differenceAt1.beforeCollection * 2,
				@$"Memory leak detected: Memory usage before garbage collection increased significantly after multiple captures.
differenceAt1: {differenceAt1.beforeCollection}
differenceAt20: {differenceAt20.beforeCollection}");
		}

		private static (long beforeCollection, long afterCollection) RunCaptureMultipleTimes(int runTimes)
		{
			using (var form = new MainForm())
			{
				var initialMemory = GC.GetTotalMemory(true);
				var captureMethod = typeof(MainForm).GetMethod("CaptureAllScreens", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

				for (int i = 0; i < runTimes; i++)
				{
					captureMethod.Invoke(form, null);
				}

				var differenceBeforeCollection = GC.GetTotalMemory(false) - initialMemory;
				GC.Collect();
				GC.WaitForPendingFinalizers();
				var differenceAfterCollection = GC.GetTotalMemory(false) - initialMemory;
				return (differenceBeforeCollection, differenceAfterCollection);
			}
		}
	}
}
