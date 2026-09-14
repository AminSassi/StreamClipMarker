using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace StreamClipMarker.Core
{
    public class HotkeyManager : NativeWindow, IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_PRIMARY_ID = 9000;
        private const int HOTKEY_LABEL_ID = 9001;

        private const uint MOD_NONE = 0x0000;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_NOREPEAT = 0x4000;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public event EventHandler HotkeyPressed;
        public event EventHandler MarkWithLabelPressed;

        private bool _isRegistered = false;

        public HotkeyManager()
        {
            CreateHandle(new CreateParams());
        }

        public bool IsRegistered
        {
            get { return _isRegistered; }
        }

        public bool Register(string keyStr, bool ctrl, bool alt, bool shift)
        {
            Unregister();

            Keys key;
            if (!Enum.TryParse(keyStr, true, out key))
            {
                key = Keys.F8;
            }

            uint modifiers = MOD_NONE;
            if (ctrl) modifiers |= MOD_CONTROL;
            if (alt) modifiers |= MOD_ALT;
            if (shift) modifiers |= MOD_SHIFT;
            modifiers |= MOD_NOREPEAT;

            bool success = RegisterHotKey(Handle, HOTKEY_PRIMARY_ID, modifiers, (uint)key);
            if (!success)
            {
                modifiers &= ~MOD_NOREPEAT;
                success = RegisterHotKey(Handle, HOTKEY_PRIMARY_ID, modifiers, (uint)key);
            }

            // Register secondary shortcut (e.g. Ctrl + Key if Ctrl not used, or Shift + Key)
            uint labelModifiers = modifiers;
            if (!ctrl)
            {
                labelModifiers |= MOD_CONTROL;
            }
            else
            {
                labelModifiers |= MOD_SHIFT;
            }

            RegisterHotKey(Handle, HOTKEY_LABEL_ID, labelModifiers, (uint)key);

            _isRegistered = success;
            return success;
        }

        public void Unregister()
        {
            if (_isRegistered && Handle != IntPtr.Zero)
            {
                try
                {
                    UnregisterHotKey(Handle, HOTKEY_PRIMARY_ID);
                    UnregisterHotKey(Handle, HOTKEY_LABEL_ID);
                }
                catch { }
                _isRegistered = false;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == HOTKEY_PRIMARY_ID)
                {
                    OnHotkeyPressed();
                }
                else if (id == HOTKEY_LABEL_ID)
                {
                    OnMarkWithLabelPressed();
                }
            }
            base.WndProc(ref m);
        }

        protected virtual void OnHotkeyPressed()
        {
            EventHandler handler = HotkeyPressed;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        protected virtual void OnMarkWithLabelPressed()
        {
            EventHandler handler = MarkWithLabelPressed;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            Unregister();
            DestroyHandle();
        }
    }
}
