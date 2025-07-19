using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VRCFaceTracking.Avalonia.Services
{
    // This thing is required to not break the ui hierarchy
    // Other methods would include passing or casting instance of main View
    // into other view (or views in the future), which would break
    // the separation of concerns
    public class DropOverlayService
    {
        public event Action<bool> ShowOverlayChanged;

        public void Show()
        {
            ShowOverlayChanged?.Invoke(true);
        }
        public void Hide()
        {
            ShowOverlayChanged?.Invoke(false);
        }

    }
}
