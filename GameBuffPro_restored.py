import tkinter as tk
from tkinter import ttk, messagebox
import pydirectinput
import random
import time
import threading
import keyboard
import sys
import ctypes
import json
import os

WM_KEYDOWN = 0x0100
WM_KEYUP = 0x0101
WM_ACTIVATE = 0x0006
WA_ACTIVE = 1

VK_CODES = {
    'f1': 0x70, 'f2': 0x71, 'f3': 0x72, 'f4': 0x73, 'f5': 0x74, 'f6': 0x75, 'f7': 0x76, 'f8': 0x77, 'f9': 0x78, 'f10': 0x79, 'f11': 0x7A, 'f12': 0x7B,
    '0': 0x30, '1': 0x31, '2': 0x32, '3': 0x33, '4': 0x34, '5': 0x35, '6': 0x36, '7': 0x37, '8': 0x38, '9': 0x39,
    'a': 0x41, 'b': 0x42, 'c': 0x43, 'd': 0x44, 'e': 0x45, 'f': 0x46, 'g': 0x47, 'h': 0x48, 'i': 0x49, 'j': 0x4A, 'k': 0x4B, 'l': 0x4C, 'm': 0x4D, 'n': 0x4E, 'o': 0x4F, 'p': 0x50, 'q': 0x51, 'r': 0x52, 's': 0x53, 't': 0x54, 'u': 0x55, 'v': 0x56, 'w': 0x57, 'x': 0x58, 'y': 0x59, 'z': 0x5A,
    'space': 0x20, 'enter': 0x0D, 'shift': 0x10, 'ctrl': 0x11, 'alt': 0x12,
    'left': 0x25, 'up': 0x26, 'right': 0x27, 'down': 0x28
}

def is_admin():
    try:
        return ctypes.windll.shell32.IsUserAnAdmin()
    except Exception:
        return False

if not is_admin():
    ctypes.windll.shell32.ShellExecuteW(None, 'runas', sys.executable, ' '.join(sys.argv), None, 1)
    sys.exit()

def get_vk_code(key_str):
    k = key_str.strip().lower()
    if k in VK_CODES:
        return VK_CODES[k]
    if len(k) == 1:
        return ord(k.upper())
    return None

def get_visible_windows():
    titles = []
    def enum_windows_callback(hwnd, lParam):
        if ctypes.windll.user32.IsWindowVisible(hwnd):
            length = ctypes.windll.user32.GetWindowTextLengthW(hwnd)
            if length > 0:
                buff = ctypes.create_unicode_buffer(length + 1)
                ctypes.windll.user32.GetWindowTextW(hwnd, buff, length + 1)
                title = buff.value
                if title:
                    titles.append(title)
        return True
    
    EnumWindowsProc = ctypes.WINFUNCTYPE(ctypes.c_bool, ctypes.c_void_p, ctypes.c_void_p)
    proc = EnumWindowsProc(enum_windows_callback)
    ctypes.windll.user32.EnumWindows(proc, 0)
    
    ignore_list = {'Program Manager', 'Settings', 'Microsoft Text Input Application', 'NVIDIA GeForce Overlay'}
    filtered_titles = []
    for t in titles:
        t_strip = t.strip()
        if t_strip and t_strip not in ignore_list:
            filtered_titles.append(t_strip)
            
    unique_titles = sorted(list(set(filtered_titles)))
    return unique_titles

def post_message_to_all(parent_hwnd, msg, wparam, lparam):
    if not parent_hwnd:
        return
    # Send activation message to parent
    try:
        ctypes.windll.user32.PostMessageW(parent_hwnd, WM_ACTIVATE, WA_ACTIVE, 0)
    except Exception:
        pass
    # Post to parent
    try:
        ctypes.windll.user32.PostMessageW(parent_hwnd, msg, wparam, lparam)
    except Exception:
        pass
    
    # Post to all child windows (essential for Unity engines, Chrome-based engines, etc.)
    def enum_child_callback(hwnd, lParam_unused):
        try:
            ctypes.windll.user32.PostMessageW(hwnd, WM_ACTIVATE, WA_ACTIVE, 0)
            ctypes.windll.user32.PostMessageW(hwnd, msg, wparam, lparam)
        except Exception:
            pass
        return True
        
    EnumChildProc = ctypes.WINFUNCTYPE(ctypes.c_bool, ctypes.c_void_p, ctypes.c_void_p)
    proc = EnumChildProc(enum_child_callback)
    ctypes.windll.user32.EnumChildWindows(parent_hwnd, proc, 0)

def get_human_delay(base_time, fluct_percent):
    if base_time <= 0:
        return (0.01, 0)
    max_delta = base_time * (fluct_percent / 100.0)
    variation = random.uniform(-max_delta, max_delta)
    actual_delay = max(0.01, base_time + variation)
    return (actual_delay, variation)

def press_key_human(key_val, base_time=1.0):
    '''模拟手指按压时长 (80ms - 180ms，小间隔时自动缩短以支持快速连点)'''
    try:
        if base_time < 0.15:
            duration = random.uniform(0.01, 0.03)
        else:
            duration = random.uniform(0.08, 0.18)
        pydirectinput.keyDown(key_val)
        time.sleep(duration)
        pydirectinput.keyUp(key_val)
    except Exception:
        pass

class ScrollableFrame(tk.Frame):
    def __init__(self, container, *args, **kwargs):
        super().__init__(container, bg='#0f0f12', *args, **kwargs)
        
        canvas = tk.Canvas(self, bg='#0f0f12', highlightthickness=0)
        scrollbar = ttk.Scrollbar(self, orient="vertical", style="TScrollbar", command=canvas.yview)
        self.scrollable_frame = tk.Frame(canvas, bg='#0f0f12')

        self.scrollable_frame.bind(
            "<Configure>",
            lambda e: canvas.configure(
                scrollregion=canvas.bbox("all")
            )
        )

        canvas.create_window((0, 0), window=self.scrollable_frame, anchor="nw")
        canvas.configure(yscrollcommand=scrollbar.set)

        def _on_mousewheel(event):
            canvas.yview_scroll(int(-1*(event.delta/120)), "units")
        
        self.scrollable_frame.bind("<Enter>", lambda _: canvas.bind_all("<MouseWheel>", _on_mousewheel))
        self.scrollable_frame.bind("<Leave>", lambda _: canvas.unbind_all("<MouseWheel>"))

        canvas.pack(side="left", fill="both", expand=True)
        scrollbar.pack(side="right", fill="y")
        
        self.canvas = canvas
        self.canvas.bind('<Configure>', self._on_canvas_configure)

    def _on_canvas_configure(self, event):
        self.canvas.itemconfig(self.canvas.find_withtag("all")[0], width=event.width)

def create_styled_entry(parent, width, default_val):
    container = tk.Frame(parent, bg='#22222c', padx=4, pady=3, highlightthickness=1, highlightbackground='#252530', bd=0)
    entry = tk.Entry(container, width=width, justify='center', bg='#22222c', fg='#ffffff', insertbackground='#ffffff', bd=0, relief='flat', font=('Arial', 9, 'bold'))
    entry.insert(0, default_val)
    entry.pack(fill='both', expand=True)
    return container, entry

class BuffCard(tk.Frame):
    def __init__(self, parent_container, app_parent, default_key="", default_time="", default_fluct="10", default_is_long=False, default_hold_time="5.0", default_is_exclusive=False):
        super().__init__(parent_container, bg='#18181f')
        self.config(highlightthickness=1, highlightbackground='#252530', highlightcolor='#252530', bd=0)
        
        self.app_parent = app_parent
        self.stop_event = threading.Event()
        
        self.columnconfigure(0, weight=1)
        self.columnconfigure(1, weight=1)
        self.columnconfigure(2, weight=1)
        
        # Header Row
        header = tk.Frame(self, bg='#18181f')
        header.grid(row=0, column=0, columnspan=3, sticky='ew', padx=10, pady=(8, 4))
        
        self.var_active = tk.BooleanVar(value=True)
        self.chk_active = tk.Checkbutton(header, text="启用此按键", variable=self.var_active, bg='#18181f', fg='#f3f4f6', selectcolor='#22222c', activebackground='#18181f', activeforeground='#f3f4f6', font=('Microsoft YaHei', 9, 'bold'), command=self.on_toggle_card)
        self.chk_active.pack(side='left')
        
        self.lbl_title = tk.Label(header, text="按键配置", font=('Microsoft YaHei', 9, 'bold'), bg='#18181f', fg='#f3f4f6')
        self.lbl_title.pack(side='left', padx=15)
        
        btn_delete = tk.Button(header, text="×", font=('Arial', 12, 'bold'), fg='#ef4444', bg='#18181f', activebackground='#18181f', activeforeground='#f43f5e', relief='flat', bd=0, cursor='hand2', command=self.delete_card)
        btn_delete.pack(side='right')
        
        # Config Row: Key, Time, Fluctuation
        self.config_frame = tk.Frame(self, bg='#18181f')
        self.config_frame.grid(row=1, column=0, columnspan=3, sticky='ew', padx=10, pady=5)
        
        # Key Entry
        col_key = tk.Frame(self.config_frame, bg='#18181f')
        col_key.pack(side='left', fill='x', expand=True, padx=2)
        self.lbl_key = tk.Label(col_key, text="按键", font=('Microsoft YaHei', 8), bg='#18181f', fg='#9ca3af')
        self.lbl_key.pack(anchor='w')
        self.frame_key, self.entry_key = create_styled_entry(col_key, 6, default_key)
        self.frame_key.pack(fill='x', pady=2)
        
        # Time Entry
        col_time = tk.Frame(self.config_frame, bg='#18181f')
        col_time.pack(side='left', fill='x', expand=True, padx=2)
        self.lbl_time = tk.Label(col_time, text="间隔 (秒)", font=('Microsoft YaHei', 8), bg='#18181f', fg='#9ca3af')
        self.lbl_time.pack(anchor='w')
        self.frame_time, self.entry_time = create_styled_entry(col_time, 8, default_time)
        self.frame_time.pack(fill='x', pady=2)
        
        # Fluctuation Entry
        col_fluct = tk.Frame(self.config_frame, bg='#18181f')
        col_fluct.pack(side='left', fill='x', expand=True, padx=2)
        self.lbl_fluct = tk.Label(col_fluct, text="波动 (%)", font=('Microsoft YaHei', 8), bg='#18181f', fg='#9ca3af')
        self.lbl_fluct.pack(anchor='w')
        self.frame_fluct, self.entry_fluct = create_styled_entry(col_fluct, 6, default_fluct)
        self.frame_fluct.pack(fill='x', pady=2)
        
        # Long Press Configuration Row
        self.long_press_frame = tk.Frame(self, bg='#18181f')
        self.long_press_frame.grid(row=2, column=0, columnspan=3, sticky='ew', padx=10, pady=2)
        
        self.var_long = tk.BooleanVar(value=default_is_long)
        self.chk_long = tk.Checkbutton(self.long_press_frame, text="长按模式", variable=self.var_long, bg='#18181f', fg='#9ca3af', selectcolor='#22222c', activebackground='#18181f', activeforeground='#f3f4f6', font=('Microsoft YaHei', 8, 'bold'), command=self.on_toggle_long_press)
        self.chk_long.pack(side='left')
        
        self.lbl_hold = tk.Label(self.long_press_frame, text="长按时长(秒):", font=('Microsoft YaHei', 8), bg='#18181f', fg='#9ca3af')
        self.lbl_hold.pack(side='left', padx=(15, 0))
        self.frame_hold, self.entry_hold = create_styled_entry(self.long_press_frame, 6, default_hold_time)
        self.frame_hold.pack(side='left', padx=6)
        
        self.var_exclusive = tk.BooleanVar(value=default_is_exclusive)
        self.chk_exclusive = tk.Checkbutton(self.long_press_frame, text="独占执行", variable=self.var_exclusive, bg='#18181f', fg='#9ca3af', selectcolor='#22222c', activebackground='#18181f', activeforeground='#f3f4f6', font=('Microsoft YaHei', 8, 'bold'))
        self.chk_exclusive.pack(side='right', padx=(10, 0))
        
        self.on_toggle_long_press()

        # Divider Line
        self.divider = tk.Frame(self, bg='#252530', height=1)
        self.divider.grid(row=3, column=0, columnspan=3, sticky='ew', padx=10, pady=5)
        
        # Monitor Row
        monitor_frame = tk.Frame(self, bg='#18181f')
        monitor_frame.grid(row=4, column=0, columnspan=3, sticky='ew', padx=10, pady=(2, 8))
        
        self.lbl_variation = tk.Label(monitor_frame, text="等待运行...", font=('Microsoft YaHei', 8), bg='#18181f', fg='#9ca3af')
        self.lbl_variation.pack(anchor='w')
        
        self.progress = ttk.Progressbar(monitor_frame, orient='horizontal', mode='determinate', style='TProgressbar')
        self.progress.pack(fill='x', pady=4)
        
        self.lbl_status = tk.Label(monitor_frame, text="就绪", font=('Microsoft YaHei', 8, 'bold'), bg='#18181f', fg='#9ca3af')
        self.lbl_status.pack(side='left')

    def on_toggle_card(self):
        state = self.var_active.get()
        if state:
            self.lbl_title.config(fg='#f3f4f6')
            self.update_status('就绪', '#9ca3af')
        else:
            self.lbl_title.config(fg='#9ca3af')
            self.update_status('已禁用', '#9ca3af')

    def on_toggle_long_press(self):
        state = self.var_long.get()
        if state:
            self.lbl_time.config(text="松开间隔 (秒)")
            self.entry_hold.config(state='normal', bg='#22222c', fg='#ffffff')
            self.frame_hold.config(bg='#22222c', highlightbackground='#252530')
        else:
            self.lbl_time.config(text="间隔 (秒)")
            self.entry_hold.config(state='disabled', bg='#1a1a24', fg='#4b5563')
            self.frame_hold.config(bg='#1a1a24', highlightbackground='#1a1a24')

    def delete_card(self):
        self.stop_logic()
        self.app_parent.remove_card(self)

    def start_logic(self, start_delay):
        if not self.var_active.get():
            self.update_status('未启用', '#9ca3af')
            return
        
        key = self.entry_key.get().strip().lower()
        try:
            base_time = float(self.entry_time.get())
            fluct_percent = float(self.entry_fluct.get())
            is_long_press = self.var_long.get()
            hold_time = float(self.entry_hold.get()) if is_long_press else 0.0
        except Exception:
            self.update_status('参数错误', '#ef4444')
            return
            
        self.stop_event.clear()
        t = threading.Thread(target=self._run, args=(key, base_time, fluct_percent, start_delay, is_long_press, hold_time))
        t.daemon = True
        t.start()

    def stop_logic(self):
        self.stop_event.set()
        if self.app_parent.exclusive_card == self:
            self.app_parent.exclusive_card = None
        
        # Safe keyUp release
        key = self.entry_key.get().strip().lower()
        self.app_parent.release_key_safe(key)
                
        self.update_status('已停止', '#ef4444')
        self.lbl_variation.config(text='等待运行...', fg='#9ca3af')
        self.progress['value'] = 0

    def check_pause(self):
        # Pause for patrol
        while self.app_parent.patrol_running and self.app_parent.var_patrol_pause.get():
            if self.stop_event.is_set():
                return True
            self.update_status_simple("⏳ 暂停中(左右巡逻)")
            time.sleep(0.1)
            
        # Pause for another exclusive key
        while self.app_parent.exclusive_card is not None and self.app_parent.exclusive_card != self:
            if self.stop_event.is_set():
                return True
            self.update_status_simple("⏳ 避让中(其他独占)")
            time.sleep(0.05)
            
        return False

    def _run(self, key, base_time, fluct_percent, start_delay, is_long_press, hold_time):
        if start_delay > 0:
            for i in range(int(start_delay), 0, -1):
                if self.stop_event.is_set():
                    return
                self.update_status(f'⏳ 倒计时 {i}s', '#3b82f6')
                time.sleep(1)
                
        # Resolve target hwnd if in bg mode
        hwnd = None
        if self.app_parent.var_bg_enabled.get():
            hwnd = self.app_parent.get_target_hwnd()
            
        while True:
            if self.stop_event.is_set():
                return
                
            # Check if we should pause before pressing
            if self.check_pause():
                return
                
            is_exclusive = self.var_exclusive.get()
            if is_exclusive:
                self.app_parent.exclusive_card = self
                time.sleep(0.05)
                
            is_bg = self.app_parent.var_bg_enabled.get()
            if is_bg and hwnd:
                # Background Posting
                vk = get_vk_code(key)
                if vk:
                    scan_code = ctypes.windll.user32.MapVirtualKeyW(vk, 0)
                    is_extended = 1 if vk in [0x25, 0x26, 0x27, 0x28] else 0
                    lparam_down = 1 | (scan_code << 16) | (is_extended << 24)
                    lparam_up = 1 | (scan_code << 16) | (is_extended << 24) | (1 << 30) | (1 << 31)
                    
                    if is_long_press:
                        self.update_status_simple('后台按住中...')
                        post_message_to_all(hwnd, WM_KEYDOWN, vk, lparam_down)
                        
                        start_hold = time.time()
                        while time.time() - start_hold < hold_time:
                            if self.stop_event.is_set():
                                post_message_to_all(hwnd, WM_KEYUP, vk, lparam_up)
                                if is_exclusive:
                                    self.app_parent.exclusive_card = None
                                return
                            time.sleep(0.1)
                            
                        post_message_to_all(hwnd, WM_KEYUP, vk, lparam_up)
                        self.update_status_simple('已松开')
                    else:
                        post_message_to_all(hwnd, WM_KEYDOWN, vk, lparam_down)
                        if base_time < 0.15:
                            duration = random.uniform(0.01, 0.03)
                        else:
                            duration = random.uniform(0.08, 0.18)
                        time.sleep(duration)
                        post_message_to_all(hwnd, WM_KEYUP, vk, lparam_up)
            else:
                # Foreground Simulation
                if is_long_press:
                    self.update_status_simple('按住中...')
                    try:
                        pydirectinput.keyDown(key)
                    except Exception:
                        pass
                        
                    start_hold = time.time()
                    while time.time() - start_hold < hold_time:
                        if self.stop_event.is_set():
                            try:
                                pydirectinput.keyUp(key)
                            except Exception:
                                pass
                            if is_exclusive:
                                self.app_parent.exclusive_card = None
                            return
                        time.sleep(0.1)
                    
                    try:
                        pydirectinput.keyUp(key)
                    except Exception:
                        pass
                    self.update_status_simple('已松开')
                else:
                    press_key_human(key, base_time)
                    
            if is_exclusive:
                self.app_parent.exclusive_card = None
                    
            actual_wait, variation = get_human_delay(base_time, fluct_percent)
            symbol = '+' if variation > 0 else ''
            var_text = f'基础:{base_time}s | 波动:{symbol}{variation:.2f}s (共 {actual_wait:.2f}s)'
            
            color = '#10b981'
            if abs(variation) > (base_time * 0.15):
                color = '#f59e0b'
                
            self.app_parent.after(0, self.update_visuals, var_text, actual_wait, base_time, color)
            
            start_wait = time.time()
            while time.time() - start_wait < actual_wait:
                if self.stop_event.is_set():
                    return
                # Check pause during sleep
                if self.check_pause():
                    return
                remaining = actual_wait - (time.time() - start_wait)
                self.app_parent.after(0, self.update_status_simple, f'下轮: {remaining:.1f}s')
                time.sleep(0.1)
                
            if self.stop_event.is_set():
                return

    def update_visuals(self, var_text, actual_wait, base_time, color):
        self.lbl_variation.config(text=var_text, fg=color)
        self.progress['maximum'] = base_time + max(5.0, base_time * 0.2)
        self.progress['value'] = actual_wait

    def update_status(self, text, color):
        self.lbl_status.config(text=text, fg=color)

    def update_status_simple(self, text):
        self.lbl_status.config(text=text)

class MainApp(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title('Artale 智能Buff助手 (Pro版)')
        self.geometry('400x670')
        self.resizable(False, False)
        self.config(bg='#0f0f12')
        
        try:
            self.iconbitmap('favicon.ico')
        except Exception:
            pass
            
        self.set_style()
        self.cards = []
        self.is_running_global = False
        self.exclusive_card = None
        self.patrol_running = False
        self.patrol_stop_event = threading.Event()
        
        # Header Frame
        header = tk.Frame(self, bg='#121214', pady=12)
        header.pack(fill='x')
        
        tk.Label(header, text='🛡️ 冒险岛 Artale 专用辅助', font=('Microsoft YaHei', 11, 'bold'), bg='#121214', fg='#f3f4f6').pack(side='left', padx=15)
        
        # Tabbed notebook structure
        notebook = ttk.Notebook(self)
        notebook.pack(padx=12, pady=6, expand=True, fill='both')
        
        # ==================== Tab 1: 常规按键 ====================
        self.tab_keys = tk.Frame(notebook, bg='#0f0f12')
        notebook.add(self.tab_keys, text="常规按键")
        
        # Global Settings Frame (in Tab 1)
        self.frame_global = tk.Frame(self.tab_keys, bg='#121214', highlightthickness=1, highlightbackground='#252530', bd=0)
        self.frame_global.pack(padx=6, pady=6, fill='x')
        
        self.fg_content = tk.Frame(self.frame_global, bg='#121214')
        self.fg_content.pack(fill='both', padx=10, pady=(8, 4))
        
        tk.Label(self.fg_content, text='启动延时 (秒):', font=('Microsoft YaHei', 9), bg='#121214', fg='#9ca3af').pack(side='left')
        
        self.frame_delay, self.entry_delay = create_styled_entry(self.fg_content, 5, '0')
        self.frame_delay.pack(side='left', padx=8)
        
        # Save config button
        self.btn_save = tk.Button(self.fg_content, text='保存配置', bg='#18181f', fg='#3b82f6', activebackground='#252530', activeforeground='#60a5fa', relief='flat', bd=0, cursor='hand2', font=('Microsoft YaHei', 8, 'bold'), padx=10, pady=4, command=self.save_config)
        self.btn_save.pack(side='left', padx=6)
        self.btn_save.bind("<Enter>", lambda e: self.btn_save.config(bg='#252530'))
        self.btn_save.bind("<Leave>", lambda e: self.btn_save.config(bg='#18181f'))
        
        tk.Label(self.fg_content, text='F9 开 / F10 关', font=('Microsoft YaHei', 8, 'bold'), bg='#121214', fg='#3b82f6').pack(side='right')
        
        # Background mode row
        self.fg_bg_content = tk.Frame(self.frame_global, bg='#121214')
        self.fg_bg_content.pack(fill='both', padx=10, pady=(4, 8))
        
        self.var_bg_enabled = tk.BooleanVar(value=False)
        self.chk_bg = tk.Checkbutton(self.fg_bg_content, text="启用后台模式", variable=self.var_bg_enabled, bg='#121214', fg='#9ca3af', selectcolor='#22222c', activebackground='#121214', activeforeground='#f3f4f6', font=('Microsoft YaHei', 8, 'bold'), command=self.on_toggle_bg_fields)
        self.chk_bg.pack(side='left')
        
        self.lbl_bg_title = tk.Label(self.fg_bg_content, text="窗口:", font=('Microsoft YaHei', 8), bg='#121214', fg='#9ca3af')
        self.lbl_bg_title.pack(side='left', padx=(10, 0))
        
        self.frame_bg_title = tk.Frame(self.fg_bg_content, bg='#22222c', padx=4, pady=3, highlightthickness=1, highlightbackground='#252530', bd=0)
        self.frame_bg_title.pack(side='left', padx=6)
        
        self.combo_bg_title = ttk.Combobox(self.frame_bg_title, width=18, font=('Arial', 9, 'bold'), postcommand=self.refresh_window_list)
        self.combo_bg_title.pack(fill='both', expand=True)
        self.combo_bg_title.set('MapleStory Worlds')
        
        self.on_toggle_bg_fields()
        
        # Scrollable Frame for cards
        self.scroll_container = ScrollableFrame(self.tab_keys)
        self.scroll_container.pack(padx=6, pady=6, expand=True, fill='both')
        
        # Button to add new card
        self.btn_add = tk.Button(self.scroll_container.scrollable_frame, text="+ 添加新按键配置", font=('Microsoft YaHei', 9, 'bold'), fg='#3b82f6', bg='#18181f', activebackground='#252530', activeforeground='#60a5fa', relief='flat', bd=0, cursor='hand2', pady=8, highlightthickness=1, highlightbackground='#252530', command=self.add_card_default)
        self.btn_add.pack(fill='x', padx=2, pady=6)
        self.btn_add.bind("<Enter>", lambda e: self.btn_add.config(bg='#252530'))
        self.btn_add.bind("<Leave>", lambda e: self.btn_add.config(bg='#18181f'))
        
        # ==================== Tab 2: 左右巡逻 ====================
        self.tab_patrol = tk.Frame(notebook, bg='#0f0f12')
        notebook.add(self.tab_patrol, text="左右巡逻")
        
        # Patrol container
        patrol_container = tk.Frame(self.tab_patrol, bg='#121214', highlightthickness=1, highlightbackground='#252530', bd=0)
        patrol_container.pack(padx=12, pady=12, fill='both', expand=True)
        
        pc = tk.Frame(patrol_container, bg='#121214', padx=15, pady=15)
        pc.pack(fill='both', expand=True)
        
        # Checkbox: Enable
        self.var_patrol_enabled = tk.BooleanVar(value=False)
        self.chk_patrol = tk.Checkbutton(pc, text="启用左右巡逻功能", variable=self.var_patrol_enabled, bg='#121214', fg='#f3f4f6', selectcolor='#22222c', activebackground='#121214', activeforeground='#f3f4f6', font=('Microsoft YaHei', 9, 'bold'), command=self.on_toggle_patrol_fields)
        self.chk_patrol.grid(row=0, column=0, columnspan=2, sticky='w', pady=10)
        
        # Input: Right duration
        tk.Label(pc, text="向右移动时长 (秒):", font=('Microsoft YaHei', 9), bg='#121214', fg='#9ca3af').grid(row=1, column=0, sticky='w', pady=6)
        self.frame_patrol_right, self.entry_patrol_right = create_styled_entry(pc, 8, '2.0')
        self.frame_patrol_right.grid(row=1, column=1, sticky='w', padx=10, pady=6)
        
        # Input: Left duration
        tk.Label(pc, text="向左移动时长 (秒):", font=('Microsoft YaHei', 9), bg='#121214', fg='#9ca3af').grid(row=2, column=0, sticky='w', pady=6)
        self.frame_patrol_left, self.entry_patrol_left = create_styled_entry(pc, 8, '2.0')
        self.frame_patrol_left.grid(row=2, column=1, sticky='w', padx=10, pady=6)
        
        # Input: Interval
        tk.Label(pc, text="循环间隔时间 (秒):", font=('Microsoft YaHei', 9), bg='#121214', fg='#9ca3af').grid(row=3, column=0, sticky='w', pady=6)
        self.frame_patrol_interval, self.entry_patrol_interval = create_styled_entry(pc, 8, '60.0')
        self.frame_patrol_interval.grid(row=3, column=1, sticky='w', padx=10, pady=6)
        
        # Input: Interval Fluctuation
        tk.Label(pc, text="间隔随机波动 (%):", font=('Microsoft YaHei', 9), bg='#121214', fg='#9ca3af').grid(row=4, column=0, sticky='w', pady=6)
        self.frame_patrol_fluct, self.entry_patrol_fluct = create_styled_entry(pc, 8, '10')
        self.frame_patrol_fluct.grid(row=4, column=1, sticky='w', padx=10, pady=6)
        
        # Checkbox: Pause others
        self.var_patrol_pause = tk.BooleanVar(value=True)
        self.chk_pause_others = tk.Checkbutton(pc, text="巡逻移动时暂停其他技能按键触发", variable=self.var_patrol_pause, bg='#121214', fg='#9ca3af', selectcolor='#22222c', activebackground='#121214', activeforeground='#f3f4f6', font=('Microsoft YaHei', 9), command=None)
        self.chk_pause_others.grid(row=5, column=0, columnspan=2, sticky='w', pady=10)
        
        # Divider
        div_p = tk.Frame(pc, bg='#252530', height=1)
        div_p.grid(row=5, column=0, columnspan=2, sticky='ew', pady=15)
        
        # Monitor
        tk.Label(pc, text="巡逻状态监控:", font=('Microsoft YaHei', 9), bg='#121214', fg='#9ca3af').grid(row=6, column=0, sticky='w')
        self.lbl_patrol_status = tk.Label(pc, text="等待运行...", font=('Microsoft YaHei', 9, 'bold'), bg='#121214', fg='#9ca3af')
        self.lbl_patrol_status.grid(row=6, column=1, sticky='w', padx=10)
        
        self.on_toggle_patrol_fields()
        
        # ==================== Tab 3: 配置预设 ====================
        self.tab_presets = tk.Frame(notebook, bg='#0f0f12')
        notebook.add(self.tab_presets, text="配置预设")
        
        # Preset Container
        preset_container = tk.Frame(self.tab_presets, bg='#121214', highlightthickness=1, highlightbackground='#252530', bd=0)
        preset_container.pack(padx=12, pady=12, fill='both', expand=True)
        
        prc = tk.Frame(preset_container, bg='#121214', padx=15, pady=15)
        prc.pack(fill='both', expand=True)
        
        # Save New Preset Section
        tk.Label(prc, text="💾 新增/覆盖预设方案", font=('Microsoft YaHei', 10, 'bold'), bg='#121214', fg='#f3f4f6').grid(row=0, column=0, columnspan=2, sticky='w', pady=(0, 10))
        
        tk.Label(prc, text="预设名称:", font=('Microsoft YaHei', 9), bg='#121214', fg='#9ca3af').grid(row=1, column=0, sticky='w', pady=6)
        self.frame_preset_name, self.entry_preset_name = create_styled_entry(prc, 18, '预设方案1')
        self.frame_preset_name.grid(row=1, column=1, sticky='w', padx=10, pady=6)
        
        self.btn_save_preset = tk.Button(prc, text='保存当前配置为该预设', bg='#18181f', fg='#10b981', activebackground='#252530', activeforeground='#34d399', relief='flat', bd=0, cursor='hand2', font=('Microsoft YaHei', 9, 'bold'), padx=15, pady=6, command=self.save_preset)
        self.btn_save_preset.grid(row=2, column=0, columnspan=2, sticky='ew', pady=(5, 15))
        self.btn_save_preset.bind("<Enter>", lambda e: self.btn_save_preset.config(bg='#252530'))
        self.btn_save_preset.bind("<Leave>", lambda e: self.btn_save_preset.config(bg='#18181f'))
        
        # Divider
        div_pr = tk.Frame(prc, bg='#252530', height=1)
        div_pr.grid(row=3, column=0, columnspan=2, sticky='ew', pady=10)
        
        # Load/Delete Section
        tk.Label(prc, text="📂 管理及加载已有预设", font=('Microsoft YaHei', 10, 'bold'), bg='#121214', fg='#f3f4f6').grid(row=4, column=0, columnspan=2, sticky='w', pady=(5, 10))
        
        tk.Label(prc, text="选择预设:", font=('Microsoft YaHei', 9), bg='#121214', fg='#9ca3af').grid(row=5, column=0, sticky='w', pady=6)
        
        self.frame_presets_combo = tk.Frame(prc, bg='#22222c', padx=4, pady=3, highlightthickness=1, highlightbackground='#252530', bd=0)
        self.frame_presets_combo.grid(row=5, column=1, sticky='w', padx=10, pady=6)
        
        self.combo_presets = ttk.Combobox(self.frame_presets_combo, width=18, font=('Arial', 9, 'bold'), state='readonly')
        self.combo_presets.pack(fill='both', expand=True)
        
        # Load button
        self.btn_load_preset = tk.Button(prc, text='📂 应用选中预设', bg='#18181f', fg='#3b82f6', activebackground='#252530', activeforeground='#60a5fa', relief='flat', bd=0, cursor='hand2', font=('Microsoft YaHei', 9, 'bold'), padx=15, pady=6, command=self.load_preset)
        self.btn_load_preset.grid(row=6, column=0, columnspan=2, sticky='ew', pady=(10, 5))
        self.btn_load_preset.bind("<Enter>", lambda e: self.btn_load_preset.config(bg='#252530'))
        self.btn_load_preset.bind("<Leave>", lambda e: self.btn_load_preset.config(bg='#18181f'))
        
        # Delete button
        self.btn_delete_preset = tk.Button(prc, text='❌ 删除选中预设', bg='#18181f', fg='#ef4444', activebackground='#252530', activeforeground='#f87171', relief='flat', bd=0, cursor='hand2', font=('Microsoft YaHei', 9, 'bold'), padx=15, pady=6, command=self.delete_preset)
        self.btn_delete_preset.grid(row=7, column=0, columnspan=2, sticky='ew', pady=5)
        self.btn_delete_preset.bind("<Enter>", lambda e: self.btn_delete_preset.config(bg='#252530'))
        self.btn_delete_preset.bind("<Leave>", lambda e: self.btn_delete_preset.config(bg='#18181f'))
        
        # Load local configurations
        self.load_config()
        
        # Bottom controls (Outside Notebook)
        frame_ctrl = tk.Frame(self, bg='#0f0f12', pady=10)
        frame_ctrl.pack(fill='x')
        
        self.btn_start = tk.Button(frame_ctrl, text='全部启动 (F9)', bg='#10b981', fg='white', activebackground='#059669', activeforeground='white', font=('Microsoft YaHei', 9, 'bold'), relief='flat', bd=0, cursor='hand2', padx=15, pady=8, command=self.start_all)
        self.btn_start.pack(side='left', padx=15, expand=True, fill='x')
        
        self.btn_stop = tk.Button(frame_ctrl, text='停止 (F10)', bg='#2d1a1e', fg='#b91c1c', activebackground='#dc2626', activeforeground='white', font=('Microsoft YaHei', 9, 'bold'), relief='flat', bd=0, cursor='hand2', padx=15, pady=8, state='disabled', command=self.stop_all)
        self.btn_stop.pack(side='right', padx=15, expand=True, fill='x')
        
        # Watermark
        watermark = tk.Label(self, text='由“HHH哥”製作 | Artale Pro v2.4', font=('Microsoft YaHei', 8), fg='#4b5563', bg='#0f0f12', pady=6)
        watermark.pack(side='bottom')
        
        self.setup_hotkeys()

    def set_style(self):
        style = ttk.Style()
        style.theme_use('clam')
        style.configure('TScrollbar', background='#18181f', troughcolor='#0f0f12', bordercolor='#252530', arrowcolor='#9ca3af')
        style.configure('TProgressbar', thickness=4, bordercolor='#252530', troughcolor='#18181f', background='#10b981')
        
        # Notebook Styling
        style.configure('TNotebook', background='#0f0f12', borderwidth=0)
        style.configure('TNotebook.Tab', background='#18181f', foreground='#9ca3af', borderwidth=0, padding=[12, 6], font=('Microsoft YaHei', 9, 'bold'))
        style.map('TNotebook.Tab',
                  background=[('selected', '#121214')],
                  foreground=[('selected', '#3b82f6')])

        # Combobox Styling
        style.configure('TCombobox', 
                        fieldbackground='#22222c', 
                        background='#18181f', 
                        foreground='#ffffff',
                        bordercolor='#252530', 
                        arrowcolor='#ffffff',
                        font=('Arial', 9, 'bold'))
        style.map('TCombobox',
                  fieldbackground=[('readonly', '#22222c'), ('disabled', '#1a1a24')],
                  foreground=[('readonly', '#ffffff'), ('disabled', '#4b5563')],
                  arrowcolor=[('disabled', '#4b5563')])
                  
        self.option_add('*TCombobox*Listbox.background', '#22222c')
        self.option_add('*TCombobox*Listbox.foreground', '#ffffff')
        self.option_add('*TCombobox*Listbox.selectBackground', '#3b82f6')
        self.option_add('*TCombobox*Listbox.selectForeground', '#ffffff')
        self.option_add('*TCombobox*Listbox.font', ('Arial', 9, 'bold'))

    def on_toggle_bg_fields(self):
        state = self.var_bg_enabled.get()
        if state:
            self.combo_bg_title.config(state='normal')
            self.frame_bg_title.config(bg='#22222c', highlightbackground='#252530')
        else:
            self.combo_bg_title.config(state='disabled')
            self.frame_bg_title.config(bg='#1a1a24', highlightbackground='#1a1a24')

    def refresh_window_list(self):
        windows = get_visible_windows()
        current_val = self.combo_bg_title.get()
        if current_val and current_val not in windows:
            windows.insert(0, current_val)
        self.combo_bg_title['values'] = windows

    def on_toggle_patrol_fields(self):
        state = self.var_patrol_enabled.get()
        if state:
            self.entry_patrol_right.config(state='normal', bg='#22222c', fg='#ffffff')
            self.frame_patrol_right.config(bg='#22222c', highlightbackground='#252530')
            self.entry_patrol_left.config(state='normal', bg='#22222c', fg='#ffffff')
            self.frame_patrol_left.config(bg='#22222c', highlightbackground='#252530')
            self.entry_patrol_interval.config(state='normal', bg='#22222c', fg='#ffffff')
            self.frame_patrol_interval.config(bg='#22222c', highlightbackground='#252530')
            self.entry_patrol_fluct.config(state='normal', bg='#22222c', fg='#ffffff')
            self.frame_patrol_fluct.config(bg='#22222c', highlightbackground='#252530')
        else:
            self.entry_patrol_right.config(state='disabled', bg='#1a1a24', fg='#4b5563')
            self.frame_patrol_right.config(bg='#1a1a24', highlightbackground='#1a1a24')
            self.entry_patrol_left.config(state='disabled', bg='#1a1a24', fg='#4b5563')
            self.frame_patrol_left.config(bg='#1a1a24', highlightbackground='#1a1a24')
            self.entry_patrol_interval.config(state='disabled', bg='#1a1a24', fg='#4b5563')
            self.frame_patrol_interval.config(bg='#1a1a24', highlightbackground='#1a1a24')
            self.entry_patrol_fluct.config(state='disabled', bg='#1a1a24', fg='#4b5563')
            self.frame_patrol_fluct.config(bg='#1a1a24', highlightbackground='#1a1a24')

    def add_card(self, key="", time_val="", fluct="10", is_long=False, hold_time="5.0", is_exclusive=False):
        card = BuffCard(self.scroll_container.scrollable_frame, self, key, time_val, fluct, is_long, hold_time, is_exclusive)
        card.pack(fill='x', padx=2, pady=4)
        self.cards.append(card)
        
        if hasattr(self, 'btn_add'):
            self.btn_add.pack_forget()
            self.btn_add.pack(fill='x', padx=2, pady=6)
            
        self.scroll_container.canvas.yview_moveto(1.0)

    def add_card_default(self):
        self.add_card('f5', '175', '10', False, '5.0', False)

    def remove_card(self, card):
        if card in self.cards:
            self.cards.remove(card)
        card.destroy()

    def get_target_hwnd(self):
        title = self.combo_bg_title.get().strip()
        if not title:
            return None
        # Try exact match first
        hwnd = ctypes.windll.user32.FindWindowW(None, title)
        if hwnd:
            return hwnd
        
        # Substring fallback
        target_hwnd = [None]
        def enum_windows_callback(hwnd, lParam):
            if ctypes.windll.user32.IsWindowVisible(hwnd):
                length = ctypes.windll.user32.GetWindowTextLengthW(hwnd)
                if length > 0:
                    buff = ctypes.create_unicode_buffer(length + 1)
                    ctypes.windll.user32.GetWindowTextW(hwnd, buff, length + 1)
                    w_title = buff.value
                    if title.lower() in w_title.lower():
                        target_hwnd[0] = hwnd
                        return False
            return True
            
        EnumWindowsProc = ctypes.WINFUNCTYPE(ctypes.c_bool, ctypes.c_void_p, ctypes.c_void_p)
        proc = EnumWindowsProc(enum_windows_callback)
        ctypes.windll.user32.EnumWindows(proc, 0)
        return target_hwnd[0]

    def release_key_safe(self, key):
        if not key:
            return
        # Release in foreground
        try:
            pydirectinput.keyUp(key)
        except Exception:
            pass
        # Release in background if enabled
        if self.var_bg_enabled.get():
            hwnd = self.get_target_hwnd()
            if hwnd:
                vk = get_vk_code(key)
                if vk:
                    scan_code = ctypes.windll.user32.MapVirtualKeyW(vk, 0)
                    is_extended = 1 if vk in [0x25, 0x26, 0x27, 0x28] else 0
                    lparam_up = 1 | (scan_code << 16) | (is_extended << 24) | (1 << 30) | (1 << 31)
                    post_message_to_all(hwnd, WM_KEYUP, vk, lparam_up)

    def start_all(self):
        if self.is_running_global:
            return
            
        # Check target window first if in background mode
        if self.var_bg_enabled.get():
            hwnd = self.get_target_hwnd()
            if not hwnd:
                messagebox.showwarning("未找到指定窗口", f"未找到标题为 '{self.combo_bg_title.get()}' 的窗口！\n请确保窗口已打开，或在‘启用后台模式’中配置正确的窗口标题。")
                return
                
        self.is_running_global = True
        self.btn_start.config(state='disabled', bg='#1e2925', fg='#047857')
        self.btn_stop.config(state='normal', bg='#ef4444', fg='white')
        
        try:
            delay = float(self.entry_delay.get())
        except Exception:
            delay = 0
            
        # Start regular timer cards
        for card in self.cards:
            card.start_logic(delay)
            
        # Start patrol sequence if enabled
        if self.var_patrol_enabled.get():
            try:
                r_time = float(self.entry_patrol_right.get())
                l_time = float(self.entry_patrol_left.get())
                interval = float(self.entry_patrol_interval.get())
                fluct_percent = float(self.entry_patrol_fluct.get())
            except Exception:
                self.update_patrol_status('参数错误', '#ef4444')
                return
                
            self.patrol_stop_event.clear()
            t_patrol = threading.Thread(target=self._run_patrol, args=(r_time, l_time, interval, fluct_percent))
            t_patrol.daemon = True
            t_patrol.start()
            
    def stop_all(self):
        if not self.is_running_global:
            return
        self.is_running_global = False
        self.exclusive_card = None
        self.btn_start.config(state='normal', bg='#10b981', fg='white')
        self.btn_stop.config(state='disabled', bg='#2d1a1e', fg='#b91c1c')
        
        # Stop regular timer cards
        for card in self.cards:
            card.stop_logic()
            
        # Stop patrol sequence
        self.patrol_stop_event.set()
        self.patrol_running = False
        
        # Safe release of movement keys (foreground and background)
        self.release_key_safe('right')
        self.release_key_safe('left')
            
        self.update_patrol_status('已停止', '#ef4444')

    def _run_patrol(self, right_time, left_time, interval, fluct_percent):
        self.update_patrol_status('就绪', '#9ca3af')
        
        hwnd = None
        if self.var_bg_enabled.get():
            hwnd = self.get_target_hwnd()
            
        while True:
            if self.patrol_stop_event.is_set():
                self.update_patrol_status('已停止', '#ef4444')
                return
                
            # Start patrol cycle
            self.patrol_running = True
            is_bg = self.var_bg_enabled.get()
            
            if is_bg and hwnd:
                # Background Patrol
                vk_right = VK_CODES['right']
                vk_left = VK_CODES['left']
                scan_right = ctypes.windll.user32.MapVirtualKeyW(vk_right, 0)
                scan_left = ctypes.windll.user32.MapVirtualKeyW(vk_left, 0)
                
                is_extended_right = 1 if vk_right in [0x25, 0x26, 0x27, 0x28] else 0
                is_extended_left = 1 if vk_left in [0x25, 0x26, 0x27, 0x28] else 0
                
                lparam_r_down = 1 | (scan_right << 16) | (is_extended_right << 24)
                lparam_r_up = 1 | (scan_right << 16) | (is_extended_right << 24) | (1 << 30) | (1 << 31)
                lparam_l_down = 1 | (scan_left << 16) | (is_extended_left << 24)
                lparam_l_up = 1 | (scan_left << 16) | (is_extended_left << 24) | (1 << 30) | (1 << 31)
                
                # Walk Right
                self.update_patrol_status('➡️ 后台向右移动...', '#10b981')
                post_message_to_all(hwnd, WM_KEYDOWN, vk_right, lparam_r_down)
                    
                start_move = time.time()
                while time.time() - start_move < right_time:
                    if self.patrol_stop_event.is_set():
                        post_message_to_all(hwnd, WM_KEYUP, vk_right, lparam_r_up)
                        self.patrol_running = False
                        self.update_patrol_status('已停止', '#ef4444')
                        return
                    time.sleep(0.1)
                    
                post_message_to_all(hwnd, WM_KEYUP, vk_right, lparam_r_up)
                    
                # Walk Left
                self.update_patrol_status('⬅️ 后台向左移动...', '#10b981')
                post_message_to_all(hwnd, WM_KEYDOWN, vk_left, lparam_l_down)
                    
                start_move = time.time()
                while time.time() - start_move < left_time:
                    if self.patrol_stop_event.is_set():
                        post_message_to_all(hwnd, WM_KEYUP, vk_left, lparam_l_up)
                        self.patrol_running = False
                        self.update_patrol_status('已停止', '#ef4444')
                        return
                    time.sleep(0.1)
                    
                post_message_to_all(hwnd, WM_KEYUP, vk_left, lparam_l_up)
            else:
                # Foreground Patrol
                # Walk Right
                self.update_patrol_status('➡️ 向右移动中...', '#10b981')
                try:
                    pydirectinput.keyDown('right')
                except Exception:
                    pass
                    
                start_move = time.time()
                while time.time() - start_move < right_time:
                    if self.patrol_stop_event.is_set():
                        try:
                            pydirectinput.keyUp('right')
                        except Exception:
                            pass
                        self.patrol_running = False
                        self.update_patrol_status('已停止', '#ef4444')
                        return
                    time.sleep(0.1)
                    
                try:
                    pydirectinput.keyUp('right')
                except Exception:
                    pass
                    
                # Walk Left
                self.update_patrol_status('⬅️ 向左移动中...', '#10b981')
                try:
                    pydirectinput.keyDown('left')
                except Exception:
                    pass
                    
                start_move = time.time()
                while time.time() - start_move < left_time:
                    if self.patrol_stop_event.is_set():
                        try:
                            pydirectinput.keyUp('left')
                        except Exception:
                            pass
                        self.patrol_running = False
                        self.update_patrol_status('已停止', '#ef4444')
                        return
                    time.sleep(0.1)
                    
                try:
                    pydirectinput.keyUp('left')
                except Exception:
                    pass
                    
            # End of cycle, allow regular keys to execute
            self.patrol_running = False
            
            # Sleep wait loop
            actual_interval, variation = get_human_delay(interval, fluct_percent)
            symbol = '+' if variation > 0 else ''
            start_wait = time.time()
            while time.time() - start_wait < actual_interval:
                if self.patrol_stop_event.is_set():
                    self.update_patrol_status('已停止', '#ef4444')
                    return
                remaining = actual_interval - (time.time() - start_wait)
                self.update_patrol_status(f'⏳ 下轮巡逻: {remaining:.1f}s (波动: {symbol}{variation:.1f}s)', '#3b82f6')
                time.sleep(0.1)

    def update_patrol_status(self, text, color):
        self.after(0, lambda: self.lbl_patrol_status.config(text=text, fg=color))

    def get_config_path(self):
        if getattr(sys, 'frozen', False):
            exe_dir = os.path.dirname(sys.executable)
        else:
            exe_dir = os.path.dirname(os.path.abspath(__file__))
        return os.path.join(exe_dir, 'config.json')

    def get_current_setup_dict(self):
        card_data = []
        for card in self.cards:
            card_data.append({
                'key': card.entry_key.get(),
                'time': card.entry_time.get(),
                'fluct': card.entry_fluct.get(),
                'enabled': card.var_active.get(),
                'is_long': card.var_long.get(),
                'hold_time': card.entry_hold.get(),
                'is_exclusive': card.var_exclusive.get()
            })
        return {
            'global_delay': self.entry_delay.get(),
            'patrol_enabled': self.var_patrol_enabled.get(),
            'patrol_right_time': self.entry_patrol_right.get(),
            'patrol_left_time': self.entry_patrol_left.get(),
            'patrol_interval': self.entry_patrol_interval.get(),
            'patrol_fluct': self.entry_patrol_fluct.get(),
            'patrol_pause_others': self.var_patrol_pause.get(),
            'bg_enabled': self.var_bg_enabled.get(),
            'bg_title': self.combo_bg_title.get(),
            'cards': card_data
        }

    def apply_config_data(self, config):
        self.entry_delay.delete(0, 'end')
        delay = config.get('global_delay', '0')
        self.entry_delay.insert(0, delay)
        
        # Load background config
        self.var_bg_enabled.set(config.get('bg_enabled', False))
        self.combo_bg_title.set(config.get('bg_title', 'MapleStory Worlds'))
        self.on_toggle_bg_fields()
        
        # Load patrol config
        self.var_patrol_enabled.set(config.get('patrol_enabled', False))
        
        self.entry_patrol_right.delete(0, 'end')
        self.entry_patrol_right.insert(0, config.get('patrol_right_time', '2.0'))
        
        self.entry_patrol_left.delete(0, 'end')
        self.entry_patrol_left.insert(0, config.get('patrol_left_time', '2.0'))
        
        self.entry_patrol_interval.delete(0, 'end')
        self.entry_patrol_interval.insert(0, config.get('patrol_interval', '60.0'))
        
        self.entry_patrol_fluct.delete(0, 'end')
        self.entry_patrol_fluct.insert(0, config.get('patrol_fluct', '10'))
        
        self.var_patrol_pause.set(config.get('patrol_pause_others', True))
        self.on_toggle_patrol_fields()
        
        for card in self.cards:
            card.destroy()
        self.cards.clear()
        
        cards_list = config.get('cards', [])
        for c_data in cards_list:
            self.add_card(
                c_data.get('key', 'f5'),
                c_data.get('time', '175'),
                c_data.get('fluct', '10'),
                c_data.get('is_long', False),
                c_data.get('hold_time', '5.0'),
                c_data.get('is_exclusive', False)
            )
            card = self.cards[-1]
            card.var_active.set(c_data.get('enabled', True))
            card.var_exclusive.set(c_data.get('is_exclusive', False))
            card.on_toggle_card()

    def save_config(self):
        setup = self.get_current_setup_dict()
        config_path = self.get_config_path()
        
        config = {}
        if os.path.exists(config_path):
            try:
                with open(config_path, 'r', encoding='utf-8') as f:
                    config = json.load(f)
            except Exception:
                pass
        
        presets = config.get('presets', {})
        config.update(setup)
        config['presets'] = presets
        
        try:
            with open(config_path, 'w', encoding='utf-8') as f:
                json.dump(config, f, indent=4, ensure_ascii=False)
            messagebox.showinfo("保存成功", "配置已成功保存！\n下次启动将自动加载当前设置。")
        except Exception as e:
            messagebox.showerror("保存失败", f"无法写入配置文件: {e}")

    def load_config(self):
        config_path = self.get_config_path()
        if not os.path.exists(config_path):
            self.add_card('f5', '175', '10', False, '5.0')
            return
            
        try:
            with open(config_path, 'r', encoding='utf-8') as f:
                config = json.load(f)
                
            self.apply_config_data(config)
            
            # Load presets list into combobox
            presets = config.get('presets', {})
            self.combo_presets['values'] = list(presets.keys())
            if presets:
                self.combo_presets.set(list(presets.keys())[0])
        except Exception as e:
            print("Failed to load config:", e)
            self.add_card('f5', '175', '10', False, '5.0')

    def save_preset(self):
        name = self.entry_preset_name.get().strip()
        if not name:
            messagebox.showwarning("参数错误", "请输入预设方案的名称！")
            return
        
        setup = self.get_current_setup_dict()
        config_path = self.get_config_path()
        
        config = {}
        if os.path.exists(config_path):
            try:
                with open(config_path, 'r', encoding='utf-8') as f:
                    config = json.load(f)
            except Exception:
                pass
        
        presets = config.get('presets', {})
        presets[name] = setup
        config['presets'] = presets
        
        try:
            with open(config_path, 'w', encoding='utf-8') as f:
                json.dump(config, f, indent=4, ensure_ascii=False)
            
            # Update combobox
            self.combo_presets['values'] = list(presets.keys())
            self.combo_presets.set(name)
            
            messagebox.showinfo("预设已保存", f"预设方案 '{name}' 已成功保存！")
        except Exception as e:
            messagebox.showerror("保存失败", f"保存预设失败: {e}")

    def load_preset(self):
        name = self.combo_presets.get().strip()
        if not name:
            messagebox.showwarning("参数错误", "请先选择要应用的预设方案！")
            return
        
        config_path = self.get_config_path()
        if not os.path.exists(config_path):
            return
        
        try:
            with open(config_path, 'r', encoding='utf-8') as f:
                config = json.load(f)
            
            presets = config.get('presets', {})
            if name not in presets:
                messagebox.showerror("未找到预设", f"找不到名为 '{name}' 的预设！")
                return
            
            # Apply preset setup
            self.apply_config_data(presets[name])
            messagebox.showinfo("应用成功", f"已成功加载并应用预设方案 '{name}'！")
        except Exception as e:
            messagebox.showerror("加载失败", f"加载预设失败: {e}")

    def delete_preset(self):
        name = self.combo_presets.get().strip()
        if not name:
            messagebox.showwarning("参数错误", "请选择要删除的预设方案！")
            return
        
        if not messagebox.askyesno("确认删除", f"确定要删除预设方案 '{name}' 吗？"):
            return
            
        config_path = self.get_config_path()
        if not os.path.exists(config_path):
            return
            
        try:
            with open(config_path, 'r', encoding='utf-8') as f:
                config = json.load(f)
            
            presets = config.get('presets', {})
            if name in presets:
                del presets[name]
            config['presets'] = presets
            
            with open(config_path, 'w', encoding='utf-8') as f:
                json.dump(config, f, indent=4, ensure_ascii=False)
            
            # Update combobox
            self.combo_presets['values'] = list(presets.keys())
            if presets:
                self.combo_presets.set(list(presets.keys())[0])
            else:
                self.combo_presets.set('')
            
            messagebox.showinfo("删除成功", f"预设方案 '{name}' 已成功删除！")
        except Exception as e:
            messagebox.showerror("删除失败", f"删除预设失败: {e}")

    def setup_hotkeys(self):
        try:
            keyboard.add_hotkey('f9', lambda: self.after(0, self.start_all))
            keyboard.add_hotkey('f10', lambda: self.after(0, self.stop_all))
        except Exception:
            pass

if __name__ == '__main__':
    app = MainApp()
    app.mainloop()
