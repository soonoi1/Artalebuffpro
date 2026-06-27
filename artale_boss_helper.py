import os
import sys
import time
import queue
import argparse
import threading
import ctypes
import webbrowser
import json
import re
import subprocess
from ctypes import windll, byref
from ctypes.wintypes import HWND, RECT, POINT
from http.server import ThreadingHTTPServer, BaseHTTPRequestHandler

import cv2
import numpy as np
import keyboard
from PIL import Image, ImageGrab, ImageDraw

import winocr
import pystray

# Global config and state
config = {
    "game_title": "MapleStory Worlds-Artale",
    "crop_log_rel_x1": 0.80,
    "crop_log_rel_x2": 1.0,
    "crop_log_rel_y1": 0.60,
    "crop_log_rel_y2": 0.92,
    
    "crop_boss_rel_x1": 0.15,
    "crop_boss_rel_x2": 0.85,
    "crop_boss_rel_y1": 0.20,
    "crop_boss_rel_y2": 0.60,
    
    "crop_exp_rel_x1": 0.40,
    "crop_exp_rel_x2": 0.78,
    "crop_exp_rel_y1": 0.90,
    "crop_exp_rel_y2": 0.99,
    
    "poll_interval_ms": 500,
    "server_port": 18290
}

state = {
    "kill_count": 0,
    "boss_spawned": False,
    "paused": False,
    "game_connected": False,
    "game_rect": (0, 0, 1024, 576), # last known game client rect (x1, y1, x2, y2)
    "history_lines": [], # list of {"text": str, "y": float, "last_seen": float, "exp": int/None}
    
    # Fallback bottom EXP bar tracker ledger variables
    "last_exp": None,
    "current_exp_val": None,
    "expected_gains": [], # list of {"amount": int, "time": float}
    "monster_exp": None,
    "monster_exp_set": [], # list of learned monster exp values in the current map
    "unreconciled_gains": [], # list of {"amount": int, "time": float}
    "log_crop_base64": "",
    "exp_crop_base64": "",
    
    "is_mock": False,
    "mock_ticks": 0,
}

# --- Win32 GDI Capture Constants & Structures ---
SRCCOPY = 0xCC0020
BI_RGB = 0

class BITMAPINFOHEADER(ctypes.Structure):
    _fields_ = [
        ('biSize', ctypes.c_uint32),
        ('biWidth', ctypes.c_int32),
        ('biHeight', ctypes.c_int32),
        ('biPlanes', ctypes.c_uint16),
        ('biBitCount', ctypes.c_uint16),
        ('biCompression', ctypes.c_uint32),
        ('biSizeImage', ctypes.c_uint32),
        ('biXPelsPerMeter', ctypes.c_int32),
        ('biYPelsPerMeter', ctypes.c_int32),
        ('biClrUsed', ctypes.c_uint32),
        ('biClrImportant', ctypes.c_uint32)
    ]

class BITMAPINFO(ctypes.Structure):
    _fields_ = [
        ('bmiHeader', BITMAPINFOHEADER),
        ('bmiColors', ctypes.c_uint32 * 3)
    ]

# --- Logging Utility ---
def log_debug(message):
    """Writes a debug log entry to debug_helper.log."""
    try:
        timestamp = time.strftime('%Y-%m-%d %H:%M:%S')
        with open("debug_helper.log", "a", encoding="utf-8") as f:
            f.write(f"[{timestamp}] {message}\n")
    except Exception:
        pass

# Clear debug log file on startup
try:
    with open("debug_helper.log", "w", encoding="utf-8") as f:
        f.write("=== Artale Boss Helper Debug Log ===\n")
except Exception:
    pass

# --- Configuration Persistence and Interactive ROI Crop Selection ---
CONFIG_FILE = "helper_config.json"

def load_local_config():
    global config
    if os.path.exists(CONFIG_FILE):
        try:
            with open(CONFIG_FILE, "r", encoding="utf-8") as f:
                loaded = json.load(f)
                config.update(loaded)
            log_debug("Loaded local config successfully.")
        except Exception as e:
            log_debug(f"Failed to load local config: {e}")

def save_local_config():
    try:
        save_keys = [
            "crop_log_rel_x1", "crop_log_rel_x2", "crop_log_rel_y1", "crop_log_rel_y2",
            "crop_exp_rel_x1", "crop_exp_rel_x2", "crop_exp_rel_y1", "crop_exp_rel_y2"
        ]
        to_save = {k: config[k] for k in save_keys if k in config}
        with open(CONFIG_FILE, "w", encoding="utf-8") as f:
            json.dump(to_save, f, indent=4)
        log_debug("Saved local config successfully.")
    except Exception as e:
        log_debug(f"Failed to save local config: {e}")

def img_to_base64(img):
    try:
        import io
        import base64
        buf = io.BytesIO()
        img.save(buf, format="JPEG", quality=80)
        return base64.b64encode(buf.getvalue()).decode('utf-8')
    except Exception as e:
        log_debug(f"img_to_base64 error: {e}")
        return ""

def interactive_crop_selection():
    log_debug("interactive_crop_selection: Starting interactive crop selection...")
    try:
        hwnd = get_game_hwnd_cached()
        if not hwnd:
            log_debug("interactive_crop_selection: Game window not found.")
            # 弹窗提示游戏未打开
            windll.user32.MessageBoxW(
                None,
                "未檢測到遊戲窗口！請先打開遊戲並進入遊戲視窗。\nGame window not found!",
                "錯誤 (Error)",
                0x10
            )
            return
            
        rect = get_client_rect_screen(hwnd)
        if not rect:
            log_debug("interactive_crop_selection: Failed to get client rect.")
            return
            
        # 截取客户端画面
        img = capture_client_area_gdi(rect)
        img_np = cv2.cvtColor(np.array(img), cv2.COLOR_RGB2BGR)
        gw, gh = img.size
        
        # 弹窗指引
        windll.user32.MessageBoxW(
            None,
            "接下來將引導您進行監控區域框選。\n\n1. 請在彈出的視窗中，用滑鼠拖曳框選「右側戰鬥日誌區」，框選後按 Enter 鍵確定。\n2. 接著在第二個彈出的視窗中，框選「底部經驗值條」，框選後按 Enter 鍵確定。\n\n如果需要取消，請在視窗中按 Esc 鍵。",
            "自定義監控區域指引",
            0x40
        )
        
        # 1. 框选战斗日志区
        prompt_img1 = img_np.copy()
        cv2.putText(prompt_img1, "Step 1: Drag to select COMBAT LOG region, then press ENTER", 
                    (20, 40), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 0, 255), 2, cv2.LINE_AA)
        
        cv2.namedWindow("Select Combat Log Region", cv2.WINDOW_AUTOSIZE)
        hwnd_win = windll.user32.FindWindowW(None, "Select Combat Log Region")
        if hwnd_win:
            windll.user32.SetWindowPos(hwnd_win, -1, 0, 0, 0, 0, 0x0001 | 0x0002) # HWND_TOPMOST
            
        r = cv2.selectROI("Select Combat Log Region", prompt_img1, fromCenter=False, showCrosshair=True)
        cv2.destroyWindow("Select Combat Log Region")
        
        if r == (0, 0, 0, 0):
            log_debug("interactive_crop_selection: Combat log selection cancelled.")
            return
            
        x, y, w, h = r
        
        # 2. 框选底部经验条区
        prompt_img2 = img_np.copy()
        cv2.putText(prompt_img2, "Step 2: Drag to select BOTTOM EXP BAR region, then press ENTER", 
                    (20, 40), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 0, 255), 2, cv2.LINE_AA)
        
        cv2.namedWindow("Select Bottom EXP Bar Region", cv2.WINDOW_AUTOSIZE)
        hwnd_win2 = windll.user32.FindWindowW(None, "Select Bottom EXP Bar Region")
        if hwnd_win2:
            windll.user32.SetWindowPos(hwnd_win2, -1, 0, 0, 0, 0, 0x0001 | 0x0002)
            
        r2 = cv2.selectROI("Select Bottom EXP Bar Region", prompt_img2, fromCenter=False, showCrosshair=True)
        cv2.destroyWindow("Select Bottom EXP Bar Region")
        
        if r2 == (0, 0, 0, 0):
            log_debug("interactive_crop_selection: Bottom EXP selection cancelled.")
            return
            
        x2, y2, w2, h2 = r2
        
        # 保存比例参数
        config["crop_log_rel_x1"] = x / gw
        config["crop_log_rel_y1"] = y / gh
        config["crop_log_rel_x2"] = (x + w) / gw
        config["crop_log_rel_y2"] = (y + h) / gh
        
        config["crop_exp_rel_x1"] = x2 / gw
        config["crop_exp_rel_y1"] = y2 / gh
        config["crop_exp_rel_x2"] = (x2 + w2) / gw
        config["crop_exp_rel_y2"] = (y2 + h2) / gh
        
        save_local_config()
        
        log_debug(f"interactive_crop_selection: Saved custom ROI percentages. Log: {config['crop_log_rel_x1']:.3f}..{config['crop_log_rel_x2']:.3f}, Exp: {config['crop_exp_rel_x1']:.3f}..{config['crop_exp_rel_x2']:.3f}")
        
        windll.user32.MessageBoxW(
            None,
            "自定義監控區域設置成功！已自動套用並保存。\nCustom monitor regions saved successfully!",
            "成功 (Success)",
            0x40
        )
    except Exception as e:
        log_debug(f"interactive_crop_selection: Exception: {e}")

# --- Single Instance Mutex ---
_mutex_handle = None
MUTEX_NAME = "Local\\ArtaleBossHelperMutex_Unique_12345"

def check_single_instance():
    """Ensures only a single instance of the helper runs using a Win32 Named Mutex."""
    global _mutex_handle
    ERROR_ALREADY_EXISTS = 183
    
    _mutex_handle = windll.kernel32.CreateMutexW(None, True, MUTEX_NAME)
    last_error = windll.kernel32.GetLastError()
    
    if last_error == ERROR_ALREADY_EXISTS:
        log_debug("check_single_instance: Another instance is already running. Exiting.")
        if _mutex_handle:
            windll.kernel32.CloseHandle(_mutex_handle)
            _mutex_handle = None
        return False
    return True

# --- DPI Scaling Helpers ---
dpi_scale = 1.0

def init_dpi():
    """Initializes per-monitor or system DPI awareness and calculates scale factor."""
    global dpi_scale
    try:
        windll.shcore.SetProcessDpiAwareness(2) # PROCESS_PER_MONITOR_DPI_AWARE
        log_debug("DPI Awareness set to Per-Monitor.")
    except Exception:
        try:
            windll.user32.SetProcessDPIAware()
            log_debug("DPI Awareness set to system-aware.")
        except Exception as e:
            log_debug(f"Failed to set DPI awareness: {e}")
            
    try:
        hdc = windll.user32.GetDC(0)
        LOGPIXELSX = 88
        dpi = windll.gdi32.GetDeviceCaps(hdc, LOGPIXELSX)
        windll.user32.ReleaseDC(0, hdc)
        dpi_scale = dpi / 96.0
        log_debug(f"System DPI: {dpi}, scale factor: {dpi_scale}")
    except Exception as e:
        log_debug(f"Failed to get DPI scale: {e}")
        dpi_scale = 1.0

# --- Win32 Window Helper Functions ---
_exclusions = ["Visual Studio Code", "Chrome", "Edge", "Firefox", "Opera", "360se", "QQBrowser", "Helper", "debug_windows", "diag_loop", "監控助手", "擊殺與", "楓之谷 Artale |", "找王助手", "Boss 監控助手"]

def get_game_hwnd():
    """Finds the game window handle by performing a case-insensitive ranking check (64-bit safe)."""
    found_windows = []
    
    def cb(hwnd, lParam):
        try:
            if windll.user32.IsWindowVisible(hwnd):
                length = windll.user32.GetWindowTextLengthW(hwnd)
                if length > 0:
                    buffer = ctypes.create_unicode_buffer(length + 1)
                    windll.user32.GetWindowTextW(hwnd, buffer, length + 1)
                    title = buffer.value
                    
                    title_lower = title.lower()
                    if any(exc.lower() in title_lower for exc in _exclusions):
                        return True
                        
                    rank = 999
                    if "artale" in title_lower and "maplestory" in title_lower:
                        rank = 1
                    elif "artale" in title_lower:
                        rank = 2
                    elif "maplestory" in title_lower and "launcher" not in title_lower and "启动" not in title_lower:
                        rank = 3
                    elif "冒险岛" in title_lower:
                        rank = 4
                        
                    if rank < 999:
                        found_windows.append((hwnd, rank, title))
        except Exception as e:
            log_debug(f"Exception in EnumWindows callback: {e}")
        return True
        
    EnumWindowsProc = ctypes.WINFUNCTYPE(ctypes.c_bool, ctypes.c_void_p, ctypes.c_void_p)
    cb_ref = EnumWindowsProc(cb)
    windll.user32.EnumWindows(cb_ref, 0)
    
    log_debug(f"get_game_hwnd: Found {len(found_windows)} potential game windows.")
    for h, r, t in found_windows:
        log_debug(f"  - HWND: {h} | Rank: {r} | Title: '{t}'")
        
    if not found_windows:
        return None
        
    found_windows.sort(key=lambda x: x[1])
    best_hwnd, best_rank, best_title = found_windows[0]
    log_debug(f"get_game_hwnd: Selected HWND: {best_hwnd} (Title: '{best_title}')")
    return best_hwnd

def get_client_rect_screen(hwnd):
    """Gets the coordinates of the client area of hwnd in screen space (DPI-aware physical pixels)."""
    if not hwnd or not windll.user32.IsWindow(hwnd):
        return None
        
    rect = RECT()
    if not windll.user32.GetClientRect(hwnd, byref(rect)):
        return None
        
    pt_topleft = POINT(0, 0)
    if not windll.user32.ClientToScreen(hwnd, byref(pt_topleft)):
        return None
        
    pt_bottomright = POINT(rect.right, rect.bottom)
    if not windll.user32.ClientToScreen(hwnd, byref(pt_bottomright)):
        return None
        
    return (pt_topleft.x, pt_topleft.y, pt_bottomright.x, pt_bottomright.y)

game_hwnd_cache = None

def get_game_hwnd_cached():
    """Gets the game window handle from cache if still valid, otherwise searches again."""
    global game_hwnd_cache
    if game_hwnd_cache and windll.user32.IsWindow(game_hwnd_cache) and windll.user32.IsWindowVisible(game_hwnd_cache):
        length = windll.user32.GetWindowTextLengthW(game_hwnd_cache)
        if length > 0:
            buffer = ctypes.create_unicode_buffer(length + 1)
            windll.user32.GetWindowTextW(game_hwnd_cache, buffer, length + 1)
            title_lower = buffer.value.lower()
            if "artale" in title_lower or "maplestory" in title_lower or "冒险岛" in title_lower:
                return game_hwnd_cache
            
    game_hwnd_cache = get_game_hwnd()
    return game_hwnd_cache

def capture_client_area_gdi(rect):
    """Captures the client area using Win32 GDI coordinates directly (bypasses PIL crop bugs)."""
    gx1, gy1, gx2, gy2 = rect
    w = gx2 - gx1
    h = gy2 - gy1
    if w <= 0 or h <= 0:
        return Image.new("RGB", (1, 1))
        
    hdc_screen = windll.user32.GetDC(0)
    hdc_mem = windll.gdi32.CreateCompatibleDC(hdc_screen)
    hbitmap = windll.gdi32.CreateCompatibleBitmap(hdc_screen, w, h)
    
    old_bitmap = windll.gdi32.SelectObject(hdc_mem, hbitmap)
    windll.gdi32.BitBlt(hdc_mem, 0, 0, w, h, hdc_screen, gx1, gy1, SRCCOPY)
    
    bmi = BITMAPINFO()
    bmi.bmiHeader.biSize = ctypes.sizeof(BITMAPINFOHEADER)
    bmi.bmiHeader.biWidth = w
    bmi.bmiHeader.biHeight = -h
    bmi.bmiHeader.biPlanes = 1
    bmi.bmiHeader.biBitCount = 32
    bmi.bmiHeader.biCompression = BI_RGB
    
    buffer = ctypes.create_string_buffer(w * h * 4)
    windll.gdi32.GetDIBits(hdc_screen, hbitmap, 0, h, buffer, byref(bmi), 0)
    
    windll.gdi32.SelectObject(hdc_mem, old_bitmap)
    windll.gdi32.DeleteObject(hbitmap)
    windll.gdi32.DeleteDC(hdc_mem)
    windll.user32.ReleaseDC(0, hdc_screen)
    
    img = Image.frombytes("RGBA", (w, h), buffer, "raw", "BGRA")
    return img.convert("RGB")

# --- OCR Processing and Counting Logic ---
def parse_exp_value(text):
    """从 OCR 文本中稳健地解析经验值整数。"""
    # 预处理清洗混淆字符
    text = text.replace(" ", "")
    
    # 过滤 HP/MP 状态栏特征 (例如 [5821/6224] 或 5821/6224，或包含 hp/mp 字样)
    text_lower = text.lower()
    if "/" in text or "hp" in text_lower or "mp" in text_lower:
        return None
        
    # 检查是否包含经验值/加号特征标志，防止误把 HP/MP 识别为经验
    has_marker = any(marker in text_lower for marker in ["exp", "ex", "ep", "％", "%", "獲", "获", "+"])
    if not has_marker:
        return None
        
    mapping = {
        "O": "0", "o": "0",
        "g": "8", "G": "6",
        "S": "5", "s": "5",
        "B": "8",
        "l": "1", "I": "1", "i": "1", "|": "1",
        "丐": "9.", "q": "9",
        "％": "%"
    }
    for k, v in mapping.items():
        text = text.replace(k, v)
        
    # 1. 匹配 EXP/EX/EP/HP/获/得 等关键字之后或者包含加号的数字
    match = re.search(r'(?:EXP|EX|EP|獲|获|\+)[\._]?(\d+)', text, re.IGNORECASE)
    if match:
        return int(match.group(1))
        
    # 2. 匹配经验条特有的: <经验值>[<百分比>%] 结构
    match = re.search(r'(\d+)[\(\[l\s]?\d+[\.,]?\d*\%', text)
    if match:
        return int(match.group(1))
        
    # 3. 如果没找到结构，提取长数字作为保底
    matches = re.findall(r'\d{4,12}', text) # 经验值一般在4位到12位之间
    if matches:
        return int(matches[0])
        
    return None

def find_best_combination(amount, exp_set):
    """
    使用广度优先搜索 (BFS) 寻找在 exp_set 怪物经验库中，最接近 amount 的非负整数击杀组合。
    返回: (best_kills, best_err)
    """
    if not exp_set:
        return 0, amount
    valid_exps = [e for e in exp_set if e >= 10]
    if not valid_exps:
        return 0, amount
        
    import collections
    best_err = abs(amount)
    best_kills = 0
    
    # queue elements: (current_sum, current_kills)
    queue = collections.deque([(0, 0)])
    visited = set()
    
    while queue:
        curr_sum, curr_kills = queue.popleft()
        err = abs(amount - curr_sum)
        
        if err < best_err:
            best_err = err
            best_kills = curr_kills
        elif err == best_err:
            # 倾向于选择更保守（更小）的击杀数
            if curr_kills < best_kills:
                best_kills = curr_kills
                
        if err == 0:
            return curr_kills, 0
            
        # 限制单次最高击杀数为 15，防爆；限制当前探索总和的上限
        if curr_kills < 15 and curr_sum < amount + min(valid_exps):
            for e in valid_exps:
                next_sum = curr_sum + e
                next_kills = curr_kills + 1
                state_key = (next_sum, next_kills)
                if state_key not in visited:
                    visited.add(state_key)
                    queue.append((next_sum, next_kills))
                    
    # 如果找到了组合，且误差在允许的 +/- 5 范围内，认为是合法匹配
    if best_err <= 5 and best_kills > 0:
        return best_kills, best_err
    return 0, amount

def clean_text_for_match(text):
    """提取文本中的汉字、字母和数字以进行相似度比对。"""
    cleaned = "".join(re.findall(r'[\u4e00-\u9fa5a-zA-Z0-9]+', text))
    return cleaned

def calculate_similarity(text1, text2):
    """计算两个清洗后文本的 Jaccard 相似度。"""
    t1 = clean_text_for_match(text1)
    t2 = clean_text_for_match(text2)
    if not t1 or not t2:
        return 0.0
    set1 = set(t1)
    set2 = set(t2)
    union = set1 | set2
    if not union:
        return 0.0
    return len(set1 & set2) / len(union)

def is_xp_line(text):
    """检查识别的文本是否属于经验值行（严格包含经验特征，排除金币和道具）。"""
    text_clean = text.replace(" ", "").lower()
    
    # 1. 白名单：必须含有经验相关特征字眼
    has_xp = any(kw in text_clean for kw in ["經", "验", "驗", "exp", "ex"])
    if not has_xp:
        return False
        
    # 2. 黑名单：若含金币、道具、装备等字眼，一律排除
    black_kws = ["金", "幣", "币", "楓", "枫", "meso", "道具", "裝備", "装备", "其他", "獲得物", "获得物"]
    if any(kw in text_clean for kw in black_kws):
        return False
        
    return True

def process_combat_log(new_ocr_lines):
    """
    使用 1D 垂直位置跟踪算法对战斗日志进行行追踪，并提取新增击杀。
    返回本次 tick 检测到的新经验增量列表。
    """
    current_xp_lines = []
    for line in new_ocr_lines:
        text = line['text']
        if is_xp_line(text):
            y_coords = [w['bounding_rect']['y'] for w in line['words']]
            y_center = np.mean(y_coords) if y_coords else 0.0
            exp_val = parse_exp_value(text)
            current_xp_lines.append({
                "text": text,
                "y": y_center,
                "exp": exp_val
            })
            
    # 按 y 坐标从小到大排序（从上到下，即从旧到新）
    current_xp_lines.sort(key=lambda x: x['y'])
    
    # 同样对历史记录按 y 坐标从小到大排序
    state["history_lines"].sort(key=lambda x: x['y'])
    
    new_gains = []
    now = time.time()
    updated_history = []
    matched_hist_indices = set()
    
    for curr in current_xp_lines:
        match_idx = -1
        for i, hist in enumerate(state["history_lines"]):
            if i in matched_hist_indices:
                continue
                
            # 追踪匹配条件：
            # 1. 行只能向上滚动或停留在相近位置 (curr_y <= hist_y + 12)
            if curr["y"] > hist["y"] + 12:
                continue
                
            # 2. 单个 tick 内的最大向上滚动距离限制在 60 像素内
            if hist["y"] - curr["y"] > 60:
                continue
                
            # 3. 如果两行都成功解析出了 EXP 数字，则 EXP 数字必须一致才能匹配
            if curr["exp"] is not None and hist["exp"] is not None:
                if curr["exp"] != hist["exp"]:
                    continue
            else:
                # 4. 否则退化到文本相似度检查
                sim = calculate_similarity(curr["text"], hist["text"])
                if sim < 0.4:
                    if abs(curr["y"] - hist["y"]) <= 6 and sim >= 0.2:
                        pass
                    else:
                        continue
                        
            # 符合所有条件，判定为同一行
            match_idx = i
            break
            
        if match_idx != -1:
            matched_hist_indices.add(match_idx)
            hist_item = state["history_lines"][match_idx]
            # 更新为当前 tick 的位置和文本
            hist_item["y"] = curr["y"]
            hist_item["text"] = curr["text"]
            if curr["exp"] is not None:
                hist_item["exp"] = curr["exp"]
            hist_item["last_seen"] = now
            updated_history.append(hist_item)
        else:
            # 没找到匹配，视为新一行的经验增加（新增击杀）
            log_debug(f"process_combat_log: 检测到新浮动文本: '{curr['text']}' (exp={curr['exp']}, y={curr['y']:.1f})")
            
            exp_val = curr["exp"]
            if exp_val is not None:
                new_gains.append(exp_val)
                if exp_val >= 10 and exp_val not in state["monster_exp_set"]:
                    state["monster_exp_set"].append(exp_val)
                    if len(state["monster_exp_set"]) > 5:
                        state["monster_exp_set"].pop(0)
                    log_debug(f"学习到新怪物 EXP: {exp_val}. 当前地图 EXP 集合: {state['monster_exp_set']}")
                
                if state["monster_exp_set"]:
                    state["monster_exp"] = min(state["monster_exp_set"])
            else:
                # 如果无法解析出数字，则使用已学到的怪物经验作为备用，否则为 0
                fallback_exp = state["monster_exp"] if state["monster_exp"] is not None else 0
                new_gains.append(fallback_exp)
                log_debug(f"无法从新行解析 EXP，使用备用经验值: {fallback_exp}")
                
            updated_history.append({
                "text": curr["text"],
                "y": curr["y"],
                "exp": curr["exp"],
                "last_seen": now
            })
            
    # 保留三秒内未匹配的历史记录
    for i, hist in enumerate(state["history_lines"]):
        if i not in matched_hist_indices:
            if now - hist["last_seen"] < 3.0:
                updated_history.append(hist)
                
    state["history_lines"] = updated_history
    return new_gains

def check_boss_spawn(new_ocr_lines):
    """检查屏幕中央区域是否包含 Boss 警告字样。"""
    boss_kws = [
        "邪惡", "氣息", "邪恶", "气息", "感受到", "出現了", "出现了", "強大", "强大", "怪物", "BOSS", "boss", 
        "警告", "WARNING", "warning", "似乎", "馬上要", "發生", "发生", "什麼事", "什么事"
    ]
    for line in new_ocr_lines:
        text = line['text'].replace(" ", "")
        if any(kw in text for kw in boss_kws):
            return True
    return False

def check_channel_change(new_ocr_lines):
    """检查屏幕中央区域是否包含换线或加载界面的特定文本。"""
    change_kws = ["前往其他世界", "正在前往", "選擇頻道", "是否要前往", "Loading", "帳號", "密碼", "記住帳號", "登入"]
    for line in new_ocr_lines:
        text = line['text'].replace(" ", "")
        if any(kw in text for kw in change_kws):
            return True
    return False

# --- Core Loop Worker Thread ---
def ocr_worker():
    """Background loop that captures screen and performs OCR."""
    log_debug("ocr_worker: Thread started.")
    
    while True:
        if os.path.exists("trigger_screenshot.txt"):
            try:
                os.remove("trigger_screenshot.txt")
                log_debug("ocr_worker: File-based screenshot trigger detected.")
                save_debug_screenshot()
            except Exception as e:
                log_debug(f"ocr_worker: Failed to process screenshot trigger: {e}")
                
        if state["paused"]:
            time.sleep(0.5)
            continue
            
        time.sleep(config["poll_interval_ms"] / 1000.0)
        
        try:
            untracked_exp_gain = 0
            exp_increased = False
            
            if state["is_mock"]:
                state["mock_ticks"] += 1
                img0_path = r"C:/Users/suhao/.gemini/antigravity/brain/60a74865-0104-4c54-85e7-8e4cbe85af8d/uploaded_image_0_1781783191456.jpg"
                img1_path = r"C:/Users/suhao/.gemini/antigravity/brain/60a74865-0104-4c54-85e7-8e4cbe85af8d/uploaded_image_1_1781783191456.png"
                
                state["game_rect"] = (100, 100, 1124, 675)
                state["game_connected"] = True
                
                if state["mock_ticks"] < 8:
                    img = Image.open(img0_path)
                else:
                    img = Image.open(img1_path)
                    
                if state["mock_ticks"] == 3:
                    log_res_lines = [{'text': '已獲得經驗值 (+142)', 'words': [{'bounding_rect': {'x': 10, 'y': 50, 'width': 100, 'height': 15}}]}]
                    exp_increased = True
                    new_exp = 43128164
                else:
                    log_res_lines = []
                    exp_increased = False
                    new_exp = 43128022
                    
                boss_res_lines = []
                if state["mock_ticks"] >= 8:
                    boss_res_lines = [{'text': '能感受到邪惡的氣息。', 'words': []}]
            else:
                hwnd = get_game_hwnd_cached()
                if not hwnd:
                    if state["game_connected"]:
                        state["game_connected"] = False
                        log_debug("ocr_worker: Game window disconnected.")
                    continue
                    
                rect = get_client_rect_screen(hwnd)
                if not rect:
                    if state["game_connected"]:
                        state["game_connected"] = False
                        log_debug("ocr_worker: Client rect failed. Disconnected.")
                    continue
                    
                if not state["game_connected"]:
                    state["game_connected"] = True
                    log_debug(f"ocr_worker: Game window CONNECTED. HWND={hwnd}, ClientRect={rect}")
                    
                state["game_rect"] = rect
                gx1, gy1, gx2, gy2 = rect
                gw, gh = gx2 - gx1, gy2 - gy1
                
                if gw <= 0 or gh <= 0:
                    continue
                
                img = capture_client_area_gdi(rect)
                
                # 1. Crop combat log (right side)
                log_x1 = int(gw * config["crop_log_rel_x1"])
                log_x2 = int(gw * config["crop_log_rel_x2"])
                log_y1 = int(gh * config["crop_log_rel_y1"])
                log_y2 = int(gh * config["crop_log_rel_y2"])
                log_crop = img.crop((log_x1, log_y1, log_x2, log_y2))
                
                # Perform OCR on log crop (revert to 3x resize, NO binarization)
                log_w, log_h = log_crop.size
                log_resized = log_crop.resize((log_w * 3, log_h * 3), Image.Resampling.LANCZOS)
                log_res = winocr.recognize_pil_sync(log_resized, 'zh-Hans')
                log_res_lines = log_res.get('lines', [])
                
                # 2. Crop boss warning region (center)
                boss_x1 = int(gw * config["crop_boss_rel_x1"])
                boss_x2 = int(gw * config["crop_boss_rel_x2"])
                boss_y1 = int(gh * config["crop_boss_rel_y1"])
                boss_y2 = int(gh * config["crop_boss_rel_y2"])
                boss_crop = img.crop((boss_x1, boss_y1, boss_x2, boss_y2))
                boss_w, boss_h = boss_crop.size
                
                # Perform OCR on boss crop (3x resize, NO binarization to retain colored warnings)
                boss_resized = boss_crop.resize((boss_w * 3, boss_h * 3), Image.Resampling.LANCZOS)
                boss_res = winocr.recognize_pil_sync(boss_resized, 'zh-Hans')
                boss_res_lines = boss_res.get('lines', [])
                
                # 3. Crop bottom EXP bar
                exp_x1 = int(gw * config["crop_exp_rel_x1"])
                exp_x2 = int(gw * config["crop_exp_rel_x2"])
                exp_y1 = int(gh * config["crop_exp_rel_y1"])
                exp_y2 = int(gh * config["crop_exp_rel_y2"])
                
                exp_diff_val = 0
                if exp_y1 > 0 and exp_y2 > exp_y1:
                    exp_crop = img.crop((exp_x1, exp_y1, exp_x2, exp_y2))
                    
                    # 实时图像流转换
                    state["log_crop_base64"] = img_to_base64(log_crop)
                    state["exp_crop_base64"] = img_to_base64(exp_crop)
                    
                    exp_w, exp_h = exp_crop.size
                    exp_resized = exp_crop.resize((exp_w * 2, exp_h * 2), Image.Resampling.LANCZOS)
                    exp_res = winocr.recognize_pil_sync(exp_resized, 'zh-Hans')
                    exp_res_lines = exp_res.get('lines', [])
                    
                    new_exp = None
                    for line in exp_res_lines:
                        val = parse_exp_value(line['text'])
                        if val is not None:
                            new_exp = val
                            break
                            
                    if new_exp is not None:
                        state["current_exp_val"] = new_exp
                        if state["last_exp"] is not None:
                            diff = new_exp - state["last_exp"]
                            if 2 < diff < 100000:
                                exp_increased = True
                                exp_diff_val = diff
                                
                                if not state["monster_exp_set"]:
                                    state["monster_exp_set"].append(diff)
                                    state["monster_exp"] = diff
                                    log_debug(f"冷启动通过底栏捕获第一个怪物 EXP: {diff}")
                            else:
                                if diff != 0:
                                    log_debug(f"EXP baseline reset/changed. Diff: {diff} ({state['last_exp']} -> {new_exp})")
                        state["last_exp"] = new_exp
            
            # --- Mathematically Synchronized Ledger logic ---
            # 1. Process combat log kills
            new_gains = process_combat_log(log_res_lines)
            new_kills = len(new_gains)
            for val in new_gains:
                state["expected_gains"].append({
                    "amount": val,
                    "time": time.time()
                })
            if new_kills > 0:
                log_debug(f"Combat log matched {new_kills} new kills. New gains expected: {new_gains}")
                
            # 2. Process bottom EXP bar difference
            if exp_increased:
                state["unreconciled_gains"].append({
                    "amount": exp_diff_val,
                    "time": time.time()
                })
                log_debug(f"Recorded unreconciled gain: +{exp_diff_val}")
                
            # 3. Perform reconciliation (consume gains using expected exp from combat log)
            total_expected = sum(g["amount"] for g in state["expected_gains"])
            if total_expected > 0 and state["unreconciled_gains"]:
                reconciled_unreconciled_indices = []
                for i, gain in enumerate(state["unreconciled_gains"]):
                    if total_expected <= 0:
                        break
                    if gain["amount"] <= total_expected:
                        total_expected -= gain["amount"]
                        reconciled_unreconciled_indices.append(i)
                    else:
                        gain["amount"] -= total_expected
                        total_expected = 0
                        
                for index in sorted(reconciled_unreconciled_indices, reverse=True):
                    state["unreconciled_gains"].pop(index)
                    
                consumed = sum(g["amount"] for g in state["expected_gains"]) - total_expected
                reconciled_expected_indices = []
                for i, g in enumerate(state["expected_gains"]):
                    if consumed <= 0:
                        break
                    if g["amount"] <= consumed:
                        consumed -= g["amount"]
                        reconciled_expected_indices.append(i)
                    else:
                        g["amount"] -= consumed
                        consumed = 0
                for index in sorted(reconciled_expected_indices, reverse=True):
                    state["expected_gains"].pop(index)
            
            # 4. Clean expired expected gains (3.0s decay)
            now = time.time()
            state["expected_gains"] = [g for g in state["expected_gains"] if now - g["time"] < 3.0]
            
            # 5. Check for expired unreconciled gains (missed kills fallback)
            extra_kills = 0
            expired_indices = []
            
            for i, gain in enumerate(state["unreconciled_gains"]):
                # Experience gains waiting for > 2.0 seconds without log matches are considered missed kills
                if now - gain["time"] > 2.0:
                    expired_indices.append(i)
                    # 优先采用多怪物组合求解器
                    kills, err = find_best_combination(gain["amount"], state["monster_exp_set"])
                    if kills > 0:
                        log_debug(f"Ledger fallback: expired gain +{gain['amount']} matched as {kills} missed kills via combination solver (error={err}). EXP Set: {state['monster_exp_set']}")
                    else:
                        # 退化为单只最小经验来保底计算
                        if state["monster_exp"] is not None and state["monster_exp"] > 0:
                            kills = int(round(gain["amount"] / state["monster_exp"]))
                            if kills < 1:
                                if gain["amount"] >= state["monster_exp"] * 0.5:
                                    kills = 1
                                else:
                                    kills = 0
                            if kills > 0:
                                log_debug(f"Ledger fallback (min exp): expired gain +{gain['amount']} matched as {kills} missed kills based on min exp {state['monster_exp']}.")
                        else:
                            kills = 1
                            log_debug(f"Ledger fallback (cold start): expired gain +{gain['amount']} matched as 1 missed kill.")
                    extra_kills += kills
                    
            for index in sorted(expired_indices, reverse=True):
                state["unreconciled_gains"].pop(index)
                
            # 6. Add kills to count
            added_kills = new_kills + extra_kills
            if added_kills > 0:
                state["kill_count"] += added_kills
                log_debug(f"Total kills added this tick: {added_kills} (new_kills={new_kills}, extra_kills={extra_kills}). Current total: {state['kill_count']}")
                
            # 7. Check for channel change or game reload screen to auto-reset count
            if check_channel_change(boss_res_lines):
                if state["kill_count"] > 0 or state["boss_spawned"] or state["history_lines"]:
                    log_debug("ocr_worker: Channel change or loading screen detected. Auto-resetting counter.")
                    reset_counter()
                    
            is_boss = check_boss_spawn(boss_res_lines)
            if is_boss and not state["boss_spawned"]:
                state["boss_spawned"] = True
                log_debug("ocr_worker: Boss spawned event detected!")
                
        except Exception as e:
            import traceback
            try:
                with open("ocr_error.log", "a", encoding="utf-8") as f:
                    f.write(f"Error in OCR loop: {e}\n")
                    f.write(traceback.format_exc() + "\n")
            except Exception:
                pass
            log_debug(f"ocr_worker: Exception in OCR loop: {e}")

# --- Helper Functions ---
def get_resource_path(relative_path):
    if hasattr(sys, '_MEIPASS'):
        return os.path.join(sys._MEIPASS, relative_path)
    return os.path.join(os.path.abspath("."), relative_path)

def save_debug_screenshot():
    """Captures the current client rect and saves crops for visual diagnostics."""
    log_debug("save_debug_screenshot: Capturing diagnostics...")
    try:
        hwnd = get_game_hwnd_cached()
        if not hwnd:
            return
        rect = get_client_rect_screen(hwnd)
        if not rect:
            return
        gx1, gy1, gx2, gy2 = rect
        gw, gh = gx2 - gx1, gy2 - gy1
        
        full_screen = capture_client_area_gdi(rect)
        log_x1 = int(gw * config["crop_log_rel_x1"])
        log_x2 = int(gw * config["crop_log_rel_x2"])
        log_y1 = int(gh * config["crop_log_rel_y1"])
        log_y2 = int(gh * config["crop_log_rel_y2"])
        log_crop = full_screen.crop((log_x1, log_y1, log_x2, log_y2))
        
        boss_x1 = int(gw * config["crop_boss_rel_x1"])
        boss_x2 = int(gw * config["crop_boss_rel_x2"])
        boss_y1 = int(gh * config["crop_boss_rel_y1"])
        boss_y2 = int(gh * config["crop_boss_rel_y2"])
        boss_crop = full_screen.crop((boss_x1, boss_y1, boss_x2, boss_y2))
        
        exp_x1 = int(gw * config["crop_exp_rel_x1"])
        exp_x2 = int(gw * config["crop_exp_rel_x2"])
        exp_y1 = int(gh * config["crop_exp_rel_y1"])
        exp_y2 = int(gh * config["crop_exp_rel_y2"])
        exp_crop = full_screen.crop((exp_x1, exp_y1, exp_x2, exp_y2))
        
        full_screen.save("debug_full_client.png")
        log_crop.save("debug_log_crop.png")
        boss_crop.save("debug_boss_crop.png")
        exp_crop.save("debug_exp_crop.png")
        
        draw = ImageDraw.Draw(full_screen)
        draw.rectangle([log_x1, log_y1, log_x2, log_y2], outline="red", width=3)
        draw.rectangle([boss_x1, boss_y1, boss_x2, boss_y2], outline="blue", width=3)
        draw.rectangle([exp_x1, exp_y1, exp_x2, exp_y2], outline="green", width=3)
        full_screen.save("debug_full_rects.png")
        
        log_debug(f"save_debug_screenshot: Saved successfully! Size={(gw, gh)}")
    except Exception as e:
        log_debug(f"save_debug_screenshot: Exception occurred: {e}")

def reset_counter():
    """F8/UI Callback: Resets counter to 0."""
    log_debug("Action: Resetting counter.")
    state["kill_count"] = 0
    state["boss_spawned"] = False
    state["history_lines"] = []
    state["last_exp"] = None
    state["current_exp_val"] = None
    state["expected_gains"] = []
    state["monster_exp"] = None
    state["monster_exp_set"] = []
    state["unreconciled_gains"] = []
    state["log_crop_base64"] = ""
    state["exp_crop_base64"] = ""

def toggle_pause():
    """F10/UI Callback: Pauses or resumes monitoring."""
    state["paused"] = not state["paused"]
    log_debug(f"Action: Paused = {state['paused']}")

def add_manual_kill():
    """UI Callback: Manually simulates a kill."""
    state["kill_count"] += 1
    log_debug(f"Action: Manually added kill. Total: {state['kill_count']}")

def exit_program():
    """Cleanly terminates the application and all threads."""
    log_debug("exit_program: Shutting down helper...")
    global tray_icon
    if tray_icon:
        try:
            tray_icon.stop()
        except Exception:
            pass
    try:
        keyboard.unhook_all()
    except Exception:
        pass
    os._exit(0)

# --- System Tray Setup ---
tray_icon = None

def create_default_icon_image():
    image = Image.new('RGBA', (64, 64), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.ellipse((4, 4, 60, 60), fill=(24, 24, 27, 255), outline=(0, 210, 255, 255), width=4)
    draw.line([(22, 46), (32, 18), (42, 46)], fill=(255, 49, 49, 255), width=5)
    draw.line([(26, 38), (38, 38)], fill=(0, 210, 255, 255), width=4)
    return image

def setup_tray():
    global tray_icon
    
    icon_path = get_resource_path("artale_boss_icon.png")
    if os.path.exists(icon_path):
        try:
            image = Image.open(icon_path)
        except Exception:
            image = create_default_icon_image()
    else:
        image = create_default_icon_image()
        
    def on_exit(icon, item):
        icon.stop()
        exit_program()
        
    def on_open_web(icon, item):
        launch_edge_app()
        
    menu = pystray.Menu(
        pystray.MenuItem('打開監控窗口', on_open_web, default=True),
        pystray.MenuItem('一鍵重置 (F8)', lambda icon, item: reset_counter()),
        pystray.MenuItem('暫停/恢復檢測 (F10)', lambda icon, item: toggle_pause()),
        pystray.MenuItem('保存診斷截圖 (F11)', lambda icon, item: save_debug_screenshot()),
        pystray.MenuItem('退出程序', on_exit)
    )
    
    tray_icon = pystray.Icon("artale_boss_helper", image, "阿尔泰 找王助手", menu)
    threading.Thread(target=tray_icon.run, daemon=True).start()

# --- HTTP and Server-Sent Events (SSE) Web Server ---
class DashboardHandler(BaseHTTPRequestHandler):
    def log_message(self, format, *args):
        pass

    def do_GET(self):
        if self.path == '/' or self.path == '/index.html':
            self.send_response(200)
            self.send_header('Content-Type', 'text/html; charset=utf-8')
            self.end_headers()
            
            html_path = get_resource_path("frontend.html")
            if os.path.exists(html_path):
                with open(html_path, 'r', encoding='utf-8') as f:
                    content = f.read()
                self.wfile.write(content.encode('utf-8'))
            else:
                fallback_html = "<h1>Error: frontend.html not found!</h1>"
                self.wfile.write(fallback_html.encode('utf-8'))
                
        elif self.path == '/events':
            self.send_response(200)
            self.send_header('Content-Type', 'text/event-stream')
            self.send_header('Cache-Control', 'no-cache')
            self.send_header('Connection', 'keep-alive')
            self.end_headers()
            
            last_sent_state = {}
            while True:
                current_state = {
                    "kill_count": state["kill_count"],
                    "boss_spawned": state["boss_spawned"],
                    "paused": state["paused"],
                    "game_connected": state["game_connected"],
                    "history": [line["text"] for line in state["history_lines"]][-15:],
                    "current_exp": state.get("current_exp_val"),
                    "monster_exp_set": state.get("monster_exp_set", []),
                    "log_crop_base64": state.get("log_crop_base64", ""),
                    "exp_crop_base64": state.get("exp_crop_base64", "")
                }
                
                if current_state != last_sent_state:
                    try:
                        self.wfile.write(f"data: {json.dumps(current_state)}\n\n".encode('utf-8'))
                        self.wfile.flush()
                        last_sent_state = current_state
                    except Exception:
                        break
                time.sleep(0.1)
                
        elif self.path == '/api/reset':
            reset_counter()
            self.send_response(200)
            self.send_header('Content-Type', 'application/json')
            self.end_headers()
            self.wfile.write(b'{"status":"ok"}')
            
        elif self.path == '/api/pause':
            toggle_pause()
            self.send_response(200)
            self.send_header('Content-Type', 'application/json')
            self.end_headers()
            self.wfile.write(b'{"status":"ok"}')
            
        elif self.path == '/api/add_kill':
            add_manual_kill()
            self.send_response(200)
            self.send_header('Content-Type', 'application/json')
            self.end_headers()
            self.wfile.write(b'{"status":"ok"}')
            
        elif self.path == '/api/start_crop_select':
            threading.Thread(target=interactive_crop_selection, daemon=True).start()
            self.send_response(200)
            self.send_header('Content-Type', 'application/json')
            self.end_headers()
            self.wfile.write(b'{"status":"ok"}')
            
        else:
            self.send_error(404)

def run_web_server():
    server_address = ('127.0.0.1', config["server_port"])
    try:
        httpd = ThreadingHTTPServer(server_address, DashboardHandler)
        log_debug(f"run_web_server: Web server running on http://127.0.0.1:{config['server_port']}")
        httpd.serve_forever()
    except Exception as e:
        log_debug(f"run_web_server: Exception in web server: {e}")

# --- Launch Edge in App Mode (Standalone Client Window) ---
def launch_edge_app():
    """Launches Microsoft Edge in App Mode targeting the local web dashboard with standalone window size."""
    try:
        # 使用独立的 user-data-dir，确保窗口尺寸独立、不受已有 Edge 最大化或缓存状态的干扰
        local_appdata = os.environ.get("LOCALAPPDATA")
        if local_appdata:
            user_data_path = os.path.join(local_appdata, "ArtaleBossHelper", "EdgeProfile")
        else:
            user_data_path = os.path.join(os.path.abspath("."), ".edge_profile")
            
        cmd = [
            'cmd.exe', '/c', 'start', 'msedge.exe', 
            f'--app=http://127.0.0.1:{config["server_port"]}', 
            '--window-size=520,740', 
            f'--user-data-dir={user_data_path}'
        ]
        subprocess.Popen(cmd, shell=True)
        log_debug(f"launch_edge_app: Edge App Mode window opened. Profile={user_data_path}")
    except Exception as e:
        log_debug(f"launch_edge_app: Failed to open Edge app mode: {e}. Opening default browser.")
        webbrowser.open(f"http://127.0.0.1:{config['server_port']}")

# --- Main Program Entry ---
if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="MapleStory Artale Boss Helper")
    parser.add_argument("--mock", action="store_true", help="Run in mock/simulation mode using static images.")
    args = parser.parse_args()
    
    log_debug("main: Starting helper app...")
    
    if not check_single_instance():
        try:
            windll.user32.MessageBoxW(
                None,
                "阿爾泰 找王助手 已經在運行中！請檢查系統右下角托盤。\nAnother instance is already running!",
                "提示 (Notification)",
                0x30
            )
        except Exception as e:
            log_debug(f"main: MessageBox failed: {e}")
        sys.exit(0)
        
    init_dpi()
    load_local_config()
    
    if args.mock:
        state["is_mock"] = True
        log_debug("main: Mock mode active.")
        print("Starting in mock/simulation mode.")
        
    # Keyboard Global Hotkeys
    keyboard.add_hotkey('F8', reset_counter)
    keyboard.add_hotkey('F10', toggle_pause)
    keyboard.add_hotkey('F11', save_debug_screenshot)
    
    # Start Web Server Thread
    server_thread = threading.Thread(target=run_web_server, daemon=True)
    server_thread.start()
    
    # Start OCR Loop Thread
    worker_thread = threading.Thread(target=ocr_worker, daemon=True)
    worker_thread.start()
    
    # Start Tray Menu
    setup_tray()
    
    # Wait a bit for server to boot, then auto-open standalone Edge App window
    time.sleep(0.5)
    launch_edge_app()
    
    log_debug("main: Helper initialized. Entering keep-alive loop.")
    try:
        while True:
            time.sleep(1.0)
    except KeyboardInterrupt:
        log_debug("main: KeyboardInterrupt. Exiting...")
        exit_program()
