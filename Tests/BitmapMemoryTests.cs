using System;
using Xunit;

namespace WindowsScreenLogger.Tests
{
    public class BitmapMemoryTests
    {
        [Fact]
        public void CaptureAllScreens_ShouldNotLeakMemory()
		{
			var differenceAt1 = RunCaptureMultipleTimes(1);
			var differenceAt20 = RunCaptureMultipleTimes(10);

			// After garbage collection, memory should be same on both
			Assert.True(differenceAt20.afterCollection < differenceAt1.afterCollection * 2 + 200, 
				$@"Memory leak detected: Memory usage after garbage collection increased significantly after multiple captures.
differenceAt1: {differenceAt1.afterCollection}
differenceAt20: {differenceAt20.afterCollection}");

			Assert.True(differenceAt20.beforeCollection < differenceAt1.beforeCollection * 2 + 200,
				@$"Memory leak detected: Memory usage before garbage collection increased significantly after multiple captures.
differenceAt1: {differenceAt1.beforeCollection}
differenceAt20: {differenceAt20.beforeCollection}");
		}

		private static (long beforeCollection, long afterCollection) RunCaptureMultipleTimes(int runTimes)
		{
			using (var form = new MainForm())
			{
				var captureMethod = typeof(MainForm).GetMethod("CaptureAllScreens", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

				var initialMemory = GC.GetTotalMemory(true);
				for (int i = 0; i < runTimes; i++)
				{
					captureMethod.Invoke(form, null);
					//DummyTest(runTimes); // Dummy test to simulate memory usage
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
