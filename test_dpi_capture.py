import os
import sys
import ctypes
from ctypes import windll, byref
from ctypes.wintypes import RECT, POINT
from PIL import Image, ImageGrab, ImageDraw
import winocr

# Enable DPI Awareness first
try:
    ctypes.windll.shcore.SetProcessDpiAwareness(2) # PROCESS_PER_MONITOR_DPI_AWARE
    print("DPI Awareness set to Per-Monitor.")
except Exception:
    try:
        ctypes.windll.user32.SetProcessDPIAware()
        print("DPI Awareness set to system-aware.")
    except Exception as e:
        print(f"Failed to set DPI awareness: {e}")

_found_hwnd = [None]
_exclusions = ["Visual Studio Code", "Chrome", "Edge", "Firefox", "Opera", "360se", "QQBrowser", "Helper", "debug_windows", "test_dpi_capture"]
EnumWindowsProc = ctypes.WINFUNCTYPE(ctypes.c_bool, ctypes.c_int, ctypes.c_void_p)

def _enum_windows_cb(hwnd, lParam):
    if windll.user32.IsWindowVisible(hwnd):
        length = windll.user32.GetWindowTextLengthW(hwnd)
        if length > 0:
            buffer = ctypes.create_unicode_buffer(length + 1)
            windll.user32.GetWindowTextW(hwnd, buffer, length + 1)
            title = buffer.value
            if any(exc in title for exc in _exclusions):
                return True
            if "Artale" in title or "MapleStory Worlds" in title or "冒险岛" in title:
                _found_hwnd[0] = hwnd
                return False
    return True

_enum_cb_ref = EnumWindowsProc(_enum_windows_cb)

def get_game_hwnd():
    _found_hwnd[0] = None
    windll.user32.EnumWindows(_enum_cb_ref, 0)
    return _found_hwnd[0]

def get_client_rect_screen(hwnd):
    if not hwnd or not windll.user32.IsWindow(hwnd):
        return None
    rect = RECT()
    windll.user32.GetClientRect(hwnd, byref(rect))
    pt_topleft = POINT(0, 0)
    windll.user32.ClientToScreen(hwnd, byref(pt_topleft))
    pt_bottomright = POINT(rect.right, rect.bottom)
    windll.user32.ClientToScreen(hwnd, byref(pt_bottomright))
    return (pt_topleft.x, pt_topleft.y, pt_bottomright.x, pt_bottomright.y)

def run_test():
    hwnd = get_game_hwnd()
    if not hwnd:
        print("Error: Game window not found!")
        return
        
    length = windll.user32.GetWindowTextLengthW(hwnd)
    buffer = ctypes.create_unicode_buffer(length + 1)
    windll.user32.GetWindowTextW(hwnd, buffer, length + 1)
    title = buffer.value
    print(f"Target HWND: {hwnd}")
    print(f"Target Title: {title}")
    
    rect = get_client_rect_screen(hwnd)
    print(f"Client Rect (Screen space): {rect}")
    if not rect:
        print("Error: Could not get client rect.")
        return
        
    gx1, gy1, gx2, gy2 = rect
    gw = gx2 - gx1
    gh = gy2 - gy1
    print(f"Client width: {gw}, height: {gh}")
    
    SM_XVIRTUALSCREEN = 76
    SM_YVIRTUALSCREEN = 77
    SM_CXVIRTUALSCREEN = 78
    SM_CYVIRTUALSCREEN = 79
    
    left_offset = windll.user32.GetSystemMetrics(SM_XVIRTUALSCREEN)
    top_offset = windll.user32.GetSystemMetrics(SM_YVIRTUALSCREEN)
    virtual_w = windll.user32.GetSystemMetrics(SM_CXVIRTUALSCREEN)
    virtual_h = windll.user32.GetSystemMetrics(SM_CYVIRTUALSCREEN)
    
    print(f"Virtual Screen Left: {left_offset}, Top: {top_offset}")
    print(f"Virtual Screen Width: {virtual_w}, Height: {virtual_h}")
    
    # Capture virtual screen
    print("Grabbing virtual screen...")
    virtual_screen = ImageGrab.grab(all_screens=True)
    print(f"Grabbed virtual screen image size: {virtual_screen.size}")
    
    x1_crop = gx1 - left_offset
    y1_crop = gy1 - top_offset
    x2_crop = gx2 - left_offset
    y2_crop = gy2 - top_offset
    
    print(f"Crop coordinates relative to virtual screen image: ({x1_crop}, {y1_crop}, {x2_crop}, {y2_crop})")
    
    try:
        cropped = virtual_screen.crop((x1_crop, y1_crop, x2_crop, y2_crop))
        print(f"Cropped image size: {cropped.size}")
        cropped.save("test_dpi_crop.png")
        print("Cropped client area saved as test_dpi_crop.png")
        
        # Test drawing boundaries on the virtual screen and save it
        draw = ImageDraw.Draw(virtual_screen)
        draw.rectangle([x1_crop, y1_crop, x2_crop, y2_crop], outline="red", width=5)
        # Also crop the log area
        log_x1 = int(gw * 0.80)
        log_x2 = int(gw * 1.0)
        log_y1 = int(gh * 0.60)
        log_y2 = int(gh * 0.92)
        
        # Draw log area boundary inside the client rect
        draw.rectangle([x1_crop + log_x1, y1_crop + log_y1, x1_crop + log_x2, y1_crop + log_y2], outline="blue", width=3)
        virtual_screen.save("test_dpi_virtual_marked.png")
        print("Virtual screen marked image saved as test_dpi_virtual_marked.png")
        
        log_crop = cropped.crop((log_x1, log_y1, log_x2, log_y2))
        log_crop.save("test_dpi_log_crop.png")
        print("Log crop saved as test_dpi_log_crop.png")
        
        log_resized = log_crop.resize((log_crop.width * 3, log_crop.height * 3), Image.Resampling.LANCZOS)
        res = winocr.recognize_pil_sync(log_resized, 'zh-Hans')
        print("OCR text result on log crop:")
        print(repr(res['text']))
        for line in res['lines']:
            print(f"  Line: {repr(line['text'])}")
            
    except Exception as e:
        print(f"Error during crop/save: {e}")

if __name__ == "__main__":
    run_test()
