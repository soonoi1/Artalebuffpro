import os
import sys
import time
from PIL import Image
import numpy as np
import cv2
import winocr

sys.path.append(r"d:\VSC\mxdboss")
import artale_boss_helper as helper

def run_test():
    print("=== Starting Headless Helper Logic Test ===")
    sys.stdout.reconfigure(encoding='utf-8')
    
    img0_path = r"C:/Users/suhao/.gemini/antigravity/brain/60a74865-0104-4c54-85e7-8e4cbe85af8d/uploaded_image_0_1781783191456.jpg"
    img1_path = r"C:/Users/suhao/.gemini/antigravity/brain/60a74865-0104-4c54-85e7-8e4cbe85af8d/uploaded_image_1_1781783191456.png"
    
    # 检查图片是否存在
    if not os.path.exists(img0_path) or not os.path.exists(img1_path):
        print("Error: Mock images not found!")
        return
        
    print("Mock images located successfully.")
    
    # 1. 测试战斗日志裁剪及 1D 位置追踪匹配
    img0 = Image.open(img0_path)
    gw, gh = img0.size
    print(f"Image 0 dimensions: {gw}x{gh}")
    
    log_x1 = int(gw * helper.config["crop_log_rel_x1"])
    log_x2 = int(gw * helper.config["crop_log_rel_x2"])
    log_y1 = int(gh * helper.config["crop_log_rel_y1"])
    log_y2 = int(gh * helper.config["crop_log_rel_y2"])
    
    log_crop = img0.crop((log_x1, log_y1, log_x2, log_y2))
    log_w, log_h = log_crop.size
    log_resized = log_crop.resize((log_w * 3, log_h * 3), Image.Resampling.LANCZOS)
    
    print("Performing OCR on simulated combat log...")
    log_res = winocr.recognize_pil_sync(log_resized, 'zh-Hans')
    print(f"Raw OCR text: {repr(log_res['text'])}")
    
    # 初始化 helper state
    helper.state["history_lines"] = []
    helper.state["kill_count"] = 0
    helper.state["expected_gains"] = []
    helper.state["monster_exp"] = None
    helper.state["unreconciled_gains"] = []
    
    kills_list = helper.process_combat_log(log_res['lines'])
    kills_found = len(kills_list)
    print(f"Kills identified in first scan: {kills_found} (gains: {kills_list})")
    helper.state["kill_count"] += kills_found
    
    # 再次扫描相同数据，验证 1D 对重及去重机制
    print("Running second scan of same log to verify de-duplication...")
    kills_list_2 = helper.process_combat_log(log_res['lines'])
    kills_found_2 = len(kills_list_2)
    print(f"Kills identified in second scan (should be 0): {kills_found_2}")
    
    # 2. 测试中央 Boss 警告区域及换线检查
    img1 = Image.open(img1_path)
    gw1, gh1 = img1.size
    print(f"\nImage 1 dimensions: {gw1}x{gh1}")
    
    boss_x1 = int(gw1 * helper.config["crop_boss_rel_x1"])
    boss_x2 = int(gw1 * helper.config["crop_boss_rel_x2"])
    boss_y1 = int(gh1 * helper.config["crop_boss_rel_y1"])
    boss_y2 = int(gh1 * helper.config["crop_boss_rel_y2"])
    
    boss_crop = img1.crop((boss_x1, boss_y1, boss_x2, boss_y2))
    boss_w, boss_h = boss_crop.size
    boss_resized = boss_crop.resize((boss_w * 3, boss_h * 3), Image.Resampling.LANCZOS)
    
    print("Performing OCR on simulated boss warning region...")
    boss_res = winocr.recognize_pil_sync(boss_resized, 'zh-Hans')
    print(f"Raw OCR text: {repr(boss_res['text'])}")
    
    is_boss = helper.check_boss_spawn(boss_res['lines'])
    print(f"Boss warning detected: {is_boss}")
    
    # 测试换线检测
    mock_channel_lines = [{'text': '是否要前往13頻道？'}]
    is_channel_change = helper.check_channel_change(mock_channel_lines)
    print(f"Channel change warning check (should be True): {is_channel_change}")
    
    mock_loading_lines = [{'text': '正在前往其他世界。'}]
    is_loading_change = helper.check_channel_change(mock_loading_lines)
    print(f"Loading change check (should be True): {is_loading_change}")
    
    # 3. 测试金币与道具行的白名单和黑名单拦截
    gold_line = "已獲得金幣 (+150)"
    item_line = "已獲得其他道具"
    legit_xp = "已獲得經驗值 (+142)"
    gibberish_xp = "一 彡 《 得 經 驗"
    
    gold_check = helper.is_xp_line(gold_line)
    item_check = helper.is_xp_line(item_line)
    legit_check = helper.is_xp_line(legit_xp)
    gibberish_check = helper.is_xp_line(gibberish_xp)
    
    print(f"Gold line check (should be False): {gold_check}")
    print(f"Item line check (should be False): {item_check}")
    print(f"Legit XP check (should be True): {legit_check}")
    print(f"Gibberish XP check (should be True): {gibberish_check}")
    
    gold_passed = (gold_check == False)
    item_passed = (item_check == False)
    xp_passed = (legit_check == True and gibberish_check == True)
    
    # 4. 测试底栏 OCR 噪声预清洗与高精度解析机制
    noisy_bar = "42g4S055 [ 丐 1 ％ ]"
    parsed_bar_val = helper.parse_exp_value(noisy_bar)
    print(f"Noisy EXP bar parse check (should be 42845055): {parsed_bar_val}")
    noisy_passed = (parsed_bar_val == 42845055)
    
    # 5. 测试 HP/MP 过滤拦截
    hp_line = "HP[5821/6224]"
    mp_line = "MP[524/4356]"
    ep_misrecognized = "EP[524/4356]"
    
    hp_check = helper.parse_exp_value(hp_line)
    mp_check = helper.parse_exp_value(mp_line)
    ep_check = helper.parse_exp_value(ep_misrecognized)
    
    print(f"HP line check (should be None): {hp_check}")
    print(f"MP line check (should be None): {mp_check}")
    print(f"EP misrecognized check (should be None): {ep_check}")
    
    hp_mp_passed = (hp_check is None and mp_check is None and ep_check is None)
    
    # 6. 测试多怪物经验组合求解算法
    print("\nTesting find_best_combination solver...")
    k1, e1 = helper.find_best_combination(600, [280, 320])
    print(f"600 with [280, 320] -> kills: {k1}, err: {e1} (expected: 2, 0)")
    
    k2, e2 = helper.find_best_combination(640, [280, 320])
    print(f"640 with [280, 320] -> kills: {k2}, err: {e2} (expected: 2, 0)")
    
    k3, e3 = helper.find_best_combination(602, [280, 320])
    print(f"602 with [280, 320] -> kills: {k3}, err: {e3} (expected: 2, 2)")
    
    k4, e4 = helper.find_best_combination(5, [280, 320])
    print(f"5 with [280, 320] -> kills: {k4}, err: {e4} (expected: 0, 5)")
    
    comb_passed = (k1 == 2 and e1 == 0 and k2 == 2 and e2 == 0 and k3 == 2 and e3 == 2 and k4 == 0 and e4 == 5)
    
    print("\n=== Test Results Summary ===")
    print(f"Initial Kill Count: {helper.state['kill_count']}")
    print(f"De-duplicated count check: {'PASSED' if kills_found_2 == 0 else 'FAILED'}")
    print(f"Boss Spawn Detection: {'PASSED' if is_boss else 'FAILED'}")
    print(f"Channel Change Detection: {'PASSED' if is_channel_change and is_loading_change else 'FAILED'}")
    print(f"Gold/Item Exclusion Check: {'PASSED' if gold_passed and item_passed and xp_passed else 'FAILED'}")
    print(f"Noisy EXP Bar Recovery Check: {'PASSED' if noisy_passed else 'FAILED'}")
    print(f"HP/MP Status Bar Block Check: {'PASSED' if hp_mp_passed else 'FAILED'}")
    print(f"Multi-Monster COMB Solver Check: {'PASSED' if comb_passed else 'FAILED'}")
    print("=== Test Complete ===")

if __name__ == "__main__":
    run_test()
