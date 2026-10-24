using System.Collections.Generic;
using 窗格.Model;

namespace 窗格
{
    internal static class FenceWindowCoordinator
    {
        public static void ShowFences(IEnumerable<FenceInfo> fences)
        {
            foreach (var fence in fences)
                ShowFence(fence);
        }

        public static FenceInfo CreateFence(string name)
        {
            var fence = FenceManager.Instance.CreateFence(name);
            ShowFence(fence);
            return fence;
        }

        private static void ShowFence(FenceInfo fence)
        {
            new FenceWindow(fence).Show();
        }
    }
}
