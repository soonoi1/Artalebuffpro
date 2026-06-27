import ctypes
from ctypes import windll, byref
from ctypes.wintypes import RECT, POINT

def get_client_rect_screen(hwnd):
    if not hwnd or not windll.user32.IsWindow(hwnd):
        return None
        
    rect = RECT()
    if not windll.user32.GetClientRect(hwnd, byref(rect)):
        return None
        
    pt_topleft = POINT(0, 0)
    windll.user32.ClientToScreen(hwnd, byref(pt_topleft))
    
    pt_bottomright = POINT(rect.right, rect.bottom)
    windll.user32.ClientToScreen(hwnd, byref(pt_bottomright))
    
    return (pt_topleft.x, pt_topleft.y, pt_bottomright.x, pt_bottomright.y)

def run_debug():
    dw = ctypes.windll.user32
    EnumWindows = dw.EnumWindows
    EnumWindowsProc = ctypes.WINFUNCTYPE(ctypes.c_bool, ctypes.c_int, ctypes.c_void_p)
    GetWindowTextW = dw.GetWindowTextW
    GetWindowTextLengthW = dw.GetWindowTextLengthW
    IsWindowVisible = dw.IsWindowVisible
    
    log_lines = []
    
    def cb(hwnd, lParam):
        length = GetWindowTextLengthW(hwnd)
        if length > 0:
            buffer = ctypes.create_unicode_buffer(length + 1)
            GetWindowTextW(hwnd, buffer, length + 1)
            title = buffer.value
            
            if "artale" in title.lower():
                visible = IsWindowVisible(hwnd) != 0
                rect_win = RECT()
                dw.GetWindowRect(hwnd, byref(rect_win))
                rect_win_str = f"({rect_win.left}, {rect_win.top}, {rect_win.right}, {rect_win.bottom})"
                
                # Test get_client_rect_screen
                client_rect = get_client_rect_screen(hwnd)
                client_rect_str = str(client_rect) if client_rect else "FAILED"
                
                # Test screen capture on client rect
                try:
                    from PIL import ImageGrab
                    if client_rect:
                        img = ImageGrab.grab(bbox=client_rect)
                        capture_status = f"SUCCESS (size: {img.size})"
                    else:
                        capture_status = "N/A (No client rect)"
                except Exception as e:
                    capture_status = f"FAILED: {e}"
                
                log_line = f"HWND: {hwnd} | Visible: {visible} | WinRect: {rect_win_str} | ClientRect: {client_rect_str} | Capture: {capture_status} | Title: {title}"
                log_lines.append(log_line)
        return True
        
    EnumWindows(EnumWindowsProc(cb), 0)
    
    output_path = "debug_windows.txt"
    with open(output_path, "w", encoding="utf-8") as f:
        f.write("=== COORDINATES & CAPTURE DEBUG LOG ===\n")
        if not log_lines:
            f.write("No matching Artale windows found.\n")
        else:
            for line in log_lines:
                f.write(line + "\n")
    
    print(f"Debug log written to {output_path}")

if __name__ == "__main__":
    run_debug()
