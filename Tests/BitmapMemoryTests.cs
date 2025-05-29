using System;
using System.Drawing;
using System.Drawing.Imaging;
using Xunit;

namespace WindowsScreenLogger.Tests
{
    public class BitmapMemoryTests
    {
        [Fact]
        public void Bitmap_Dispose_ShouldNotLeakMemory()
        {
            long initialMemory = GC.GetTotalMemory(true);

            for (int i = 0; i < 50; i++)
            {
                using (var bmp = new Bitmap(800, 600))
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Red);
                }
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            long finalMemory = GC.GetTotalMemory(true);

            // Allow some fluctuation, but memory should not grow unbounded
            Assert.True(finalMemory - initialMemory < 5 * 1024 * 1024); // <5MB difference
        }
    }
}
