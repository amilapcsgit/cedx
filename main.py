import streamlit as st
import urllib.parse # Added import
import pandas as pd
import plotly.express as px
import plotly.graph_objects as go
from pathlib import Path
import json
import logging
from datetime import datetime
import os
import subprocess
import re
import concurrent.futures

from asset_parser import AssetParser
from dashboard_components import DashboardComponents

# Configure logging
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')
logger = logging.getLogger(__name__)

# Page configuration
st.set_page_config(
    page_title="IT Asset Management Dashboard",
    page_icon="🖥️",
    layout="wide",
    initial_sidebar_state="expanded"
)

# Windows 11 Theme CSS
def apply_windows11_theme():
    """Apply Modern Premium styled CSS"""
    theme_mode = st.session_state.get('theme_mode', 'light')
    
    if theme_mode == 'dark':
        bg_color = "#121212"
        surface_color = "#1e1e1e"
        card_color = "#2d2d30"
        text_color = "#e0e0e0"
        text_muted = "#a0a0a0"
        accent_color = "#3b82f6"
        hover_color = "#60a5fa"
        border_color = "#333333"
        shadow = "0 4px 6px -1px rgba(0, 0, 0, 0.5), 0 2px 4px -1px rgba(0, 0, 0, 0.3)"
        shadow_hover = "0 10px 15px -3px rgba(0, 0, 0, 0.6), 0 4px 6px -2px rgba(0, 0, 0, 0.4)"
    else:
        bg_color = "#f8fafc"
        surface_color = "#ffffff"
        card_color = "#ffffff"
        text_color = "#0f172a"
        text_muted = "#64748b"
        accent_color = "#2563eb"
        hover_color = "#1d4ed8"
        border_color = "#e2e8f0"
        shadow = "0 1px 3px 0 rgba(0, 0, 0, 0.1), 0 1px 2px 0 rgba(0, 0, 0, 0.06)"
        shadow_hover = "0 10px 15px -3px rgba(0, 0, 0, 0.1), 0 4px 6px -2px rgba(0, 0, 0, 0.05)"
    
    st.markdown(f"""
    <style>
    @import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap');

    .stApp {{
        background-color: {bg_color};
        color: {text_color};
        font-family: 'Inter', sans-serif;
    }}

    .main-title {{
        font-size: 2rem;
        font-weight: 700;
        margin-bottom: 0px;
        color: {text_color};
        letter-spacing: -0.025em;
    }}

    .caption-text {{
        font-size: 0.875rem;
        color: {text_muted};
        padding-top: 4px;
        font-weight: 500;
    }}
    
    .asset-list-card {{
        background-color: {card_color};
        border-radius: 8px;
        box-shadow: {shadow};
        border: 1px solid {border_color};
        margin-bottom: 12px;
        overflow: hidden;
        transition: all 0.2s ease-in-out;
    }}
    
    .asset-list-card:hover {{
        box-shadow: {shadow_hover};
        border-color: {hover_color};
    }}

    .status-bar-vertical {{ width: 6px; float: left; height: 100%; min-height: 120px; }}
    .status-indicator-online .status-bar-vertical {{ background-color: #10b981; }}
    .status-indicator-offline .status-bar-vertical {{ background-color: #ef4444; }}
    .status-indicator-scanning .status-bar-vertical {{ background-color: #94a3b8; }}
    .status-indicator-pending .status-bar-vertical {{ background-color: #f59e0b; }}
    .status-indicator-failed .status-bar-vertical {{ background-color: #ef4444; }}
    .status-indicator-unknown .status-bar-vertical {{ background-color: #94a3b8; }}

    .card-content-wrapper {{
        padding: 16px;
        display: flex;
        flex-direction: column;
        gap: 12px;
        margin-left: 6px;
    }}

    .card-top-row {{
        display: flex;
        justify-content: space-between;
        align-items: flex-start;
        border-bottom: 1px solid {border_color};
        padding-bottom: 8px;
    }}

    .card-title-group {{
        display: flex;
        flex-direction: column;
    }}

    .asset-name {{ font-size: 1.125rem; font-weight: 600; color: {text_color}; }}
    .asset-domain {{ font-size: 0.75rem; color: {text_muted}; }}
    .asset-ip {{ font-size: 0.875rem; color: {accent_color}; font-family: monospace; font-weight: 600; margin-top: 4px; }}

    .card-actions-group {{
        display: flex;
        flex-direction: column;
        align-items: flex-end;
        gap: 8px;
    }}

    .card-details-grid {{
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
        gap: 12px;
        font-size: 0.8125rem;
        color: {text_muted};
    }}

    .detail-item {{
        display: flex;
        align-items: center;
        gap: 6px;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
    }}

    .copy-button {{
        background: transparent;
        border: 1px solid {border_color};
        border-radius: 4px;
        color: {text_color};
        padding: 2px 6px;
        font-size: 0.7rem;
        cursor: pointer;
        transition: all 0.2s;
    }}
    .copy-button:hover {{ background: {border_color}; }}

    .expanded-metrics-grid {{
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 16px;
        padding: 12px 0;
        font-size: 0.8125rem;
    }}
    
    .expanded-column {{
        background: {surface_color};
        padding: 12px;
        border-radius: 6px;
        border: 1px solid {border_color};
    }}
    
    .metric-row {{
        display: flex;
        justify-content: space-between;
        margin-bottom: 4px;
        border-bottom: 1px dashed {border_color};
        padding-bottom: 2px;
    }}
    .metric-label {{ color: {text_muted}; font-weight: 500; }}
    .metric-value {{ color: {text_color}; font-weight: 600; text-align: right; overflow-wrap: anywhere; }}
    </style>
    """, unsafe_allow_html=True)

class ITAssetDashboard:
    def __init__(self):
        self.asset_parser = AssetParser()
        self.dashboard_components = DashboardComponents()
        self.assets_folder = Path("assets")
        
        if 'assets_data' not in st.session_state:
            st.session_state.assets_data = {}
        if 'last_refresh' not in st.session_state:
            st.session_state.last_refresh = None
        if 'theme_mode' not in st.session_state:
            st.session_state.theme_mode = 'light'
        if 'show_asset_details' not in st.session_state:
            st.session_state.show_asset_details = False
        if 'selected_asset_for_details' not in st.session_state:
            st.session_state.selected_asset_for_details = None

        if 'nmap_path' not in st.session_state:
            st.session_state.nmap_path = "nmap"
        if 'nmap_scan_type' not in st.session_state:
            st.session_state.nmap_scan_type = "Quick Scan"

        if 'selected_os_filter' not in st.session_state:
            st.session_state.selected_os_filter = []
        if 'selected_manufacturers_filter' not in st.session_state:
            st.session_state.selected_manufacturers_filter = []
        if 'ram_range_filter' not in st.session_state:
            st.session_state.ram_range_filter = None
        if 'storage_range_filter' not in st.session_state:
            st.session_state.storage_range_filter = None
        if 'show_low_storage_only' not in st.session_state:
            st.session_state.show_low_storage_only = False
        if 'anydesk_search_filter' not in st.session_state:
            st.session_state.anydesk_search_filter = ""
        if 'search_term_filter' not in st.session_state:
            st.session_state.search_term_filter = ""

        if 'show_summary_section' not in st.session_state:
            st.session_state.show_summary_section = True
        if 'show_bubbles_section' not in st.session_state:
            st.session_state.show_bubbles_section = True
        if 'show_details_table_section' not in st.session_state:
            st.session_state.show_details_table_section = True


    def _run_nmap_scan(self, ip_address: str, nmap_executable_path: str = "nmap", scan_type: str = "Full Scan") -> dict:
        result = { "status": "unknown", "mac_address": None, "nmap_output": "", "error_message": None }
        logger.info(f"Starting nmap scan for IP: {ip_address}")
        try:
            command = []
            if scan_type == "Quick Scan":
                command = [nmap_executable_path, "-sn", "-T4", ip_address]
            elif scan_type == "Full Scan":
                command = [nmap_executable_path, "-T4", "-A", "-v", "-Pn", ip_address]
            else:
                result["error_message"] = f"Invalid scan type: {scan_type}"
                logger.error(result["error_message"])
                return result

            logger.info(f"Executing Nmap {scan_type} for {ip_address}: {' '.join(command)}")
            process = subprocess.run(command, capture_output=True, text=True, timeout=120)
            result["nmap_output"] = process.stdout

            if process.returncode == 0:
                if "Host seems down" in process.stdout: result["status"] = "offline"
                elif "Host is up" in process.stdout: result["status"] = "online"
                elif scan_type == "Full Scan" and re.search(r"\d+/open/", process.stdout): result["status"] = "online"
                else: result["status"] = "offline"
                logger.info(f"Nmap {scan_type} for {ip_address}: Parsed status: {result['status']}.")
                if scan_type == "Full Scan":
                    mac_match = re.search(r"MAC Address: ([0-9A-Fa-f:]{17})", process.stdout, re.IGNORECASE)
                    if mac_match: result["mac_address"] = mac_match.group(1).upper()
            else:
                result["error_message"] = f"Nmap scan failed (code {process.returncode}): {process.stderr}"
                logger.error(result["error_message"])
        except FileNotFoundError:
            result["error_message"] = f"Nmap not found at '{nmap_executable_path}'."
            logger.error(result["error_message"])
        except subprocess.TimeoutExpired:
            result["error_message"] = "Nmap scan timed out."
            logger.error(result["error_message"])
        except Exception as e:
            result["error_message"] = f"Nmap scan error: {e}"
            logger.error(result["error_message"], exc_info=True)
        return result

    def load_assets_data(self):
       logger.info("Starting load_assets_data...")
       try:
           if not self.assets_folder.exists():
               self.assets_folder.mkdir(exist_ok=True); return {}
           asset_files = list(self.assets_folder.glob("*.txt"))
           if not asset_files: return {}
           
           assets_data = {}
           logger.info(f"Found {len(asset_files)} asset files. Processing texts...")
           
           for file_path_obj in asset_files:
               file_path_str = str(file_path_obj)
               try:
                   asset_data_item = self.asset_parser.parse_asset_file(file_path_obj)
                   if asset_data_item:
                       asset_name = asset_data_item.get('computer_name', file_path_obj.stem)
                       if 'network_info' not in asset_data_item: asset_data_item['network_info'] = {}
                       asset_data_item['network_info']['nmap_scan_status'] = 'pending_quick_scan'
                       asset_data_item['network_info']['status'] = asset_data_item['network_info'].get('status', 'unknown')
                       assets_data[asset_name] = asset_data_item
               except Exception as e:
                   logger.error(f"Error processing text for file {file_path_str}: {e}", exc_info=True)

           logger.info("Text parsing complete. Launching concurrent NMAP quick scans...")
           nmap_exe_path = st.session_state.get('nmap_path', 'nmap')

           def scan_asset(name, item):
               ip_addr = item.get('network_info', {}).get('ip_address')
               if ip_addr and ip_addr != 'N/A':
                   return name, self._run_nmap_scan(ip_addr, nmap_executable_path=nmap_exe_path, scan_type="Quick Scan")
               return name, None

           # Run nmap concurrently to prevent Streamlit hanging for minutes
           with concurrent.futures.ThreadPoolExecutor(max_workers=30) as executor:
               future_to_name = {executor.submit(scan_asset, name, item): name for name, item in assets_data.items()}
               for future in concurrent.futures.as_completed(future_to_name):
                   asset_name = future_to_name[future]
                   try:
                       name, nmap_result = future.result()
                       item = assets_data[asset_name]
                       if nmap_result is not None:
                           item['network_info']['nmap_scan_status'] = 'completed_quick_scan'
                           if nmap_result.get('status') and nmap_result.get('status') not in ['unknown', 'error']:
                               item['network_info']['status'] = nmap_result['status']
                           item['network_info']['nmap_quick_scan_output'] = nmap_result.get('nmap_output', '')
                           if nmap_result.get('error_message'):
                               item['network_info']['nmap_scan_status'] = 'failed_quick_scan'
                               item['network_info']['nmap_error'] = nmap_result['error_message']
                               logger.error(f"Nmap Quick Scan failed for {asset_name}: {nmap_result['error_message']}")
                       else:
                           item['network_info']['nmap_scan_status'] = 'skipped_no_ip'
                   except Exception as e:
                       logger.error(f"Thread scan failed for {asset_name}: {e}")

           st.session_state.last_refresh = datetime.now()
           logger.info(f"load_assets_data completed. Loaded {len(assets_data)} assets.")
           return assets_data
       except Exception as e:
           logger.error(f"Major error in load_assets_data: {e}", exc_info=True)
           st.error(f"Error loading assets data: {e}"); return {}

    def normalize_os_version(self, os_string):
        if not os_string: return "Unknown"
        os_lower = os_string.lower()
        if "windows 11" in os_lower: return "Windows 11"
        if "windows 10" in os_lower: return "Windows 10"
        if "windows 8" in os_lower: return "Windows 8"
        if "windows 7" in os_lower: return "Windows 7"
        if "windows server 2022" in os_lower: return "Windows Server 2022"
        if "windows server 2019" in os_lower: return "Windows Server 2019"
        if "windows server 2016" in os_lower: return "Windows Server 2016"
        if "windows server" in os_lower: return "Windows Server"
        return os_string

    def get_c_drive_free_space(self, asset):
        try:
            for device in asset.get('hardware_info', {}).get('storage', []):
                if ('C:' in device.get('name','').upper() or 'C DRIVE' in device.get('name','').upper()):
                    return device.get('free_space_gb')
            raw_content = asset.get('raw_content', '')
            if raw_content:
                for pattern in [r'C:.*?(\d+\.?\d*)\s*GB.*?free', r'Free Space.*?C.*?(\d+\.?\d*)\s*GB']:
                    match = re.search(pattern, raw_content, re.IGNORECASE)
                    if match: return float(match.group(1))
            return None
        except: return None

    def check_and_install_dependencies(self):
        # ... (implementation unchanged) ...
        pass

    def render_header(self):
        """Render the main header with title and refresh button"""
        col1, col2, col3, col4 = st.columns([3, 1, 1, 1])

        with col1:
            st.markdown('<p class="main-title">🖥️ IT Asset Management Dashboard</p>', unsafe_allow_html=True)
            if st.session_state.last_refresh:
                st.markdown(f'<p class="caption-text">Last updated: {st.session_state.last_refresh.strftime("%Y-%m-%d %H:%M:%S")}</p>', unsafe_allow_html=True)

        with col2:
            # Theme toggle
            if st.button("🌓 Toggle Theme"):
                st.session_state.theme_mode = 'dark' if st.session_state.theme_mode == 'light' else 'light'
                st.rerun()

        with col3:
            if st.button("Refresh Data 🔄"): # Changed button text and logic
                st.session_state.refresh_trigger = True
                st.rerun()

        with col4:
            asset_count = len(st.session_state.assets_data)
            st.metric("Total Assets", asset_count)

    def render_sidebar_filters(self):
        # ... (implementation unchanged by this subtask, but uses updated session state) ...
        st.sidebar.header("Filters & Options")
        filters = {}
        if not st.session_state.assets_data:
            st.sidebar.info("No asset data available.")
            return {
               'selected_os': [], 'selected_manufacturers': [],
               'min_ram': 0, 'max_ram': 128, 'min_storage': 0.0, 'max_storage': 500.0,
               'show_low_storage': False, 'anydesk_search': "", 'search_term': "",
               'nmap_scan_type': st.session_state.get('nmap_scan_type', "Quick Scan"),
               'nmap_path': st.session_state.get('nmap_path', "nmap")
           }
        all_os_versions_set, all_manufacturers_set, all_ram_values_list, all_storage_values_list = set(), set(), [], []
        for asset in st.session_state.assets_data.values():
            if 'os_info' in asset and asset['os_info'].get('version'): all_os_versions_set.add(self.normalize_os_version(asset['os_info']['version']))
            if 'system_info' in asset and asset['system_info'].get('manufacturer'): all_manufacturers_set.add(asset['system_info']['manufacturer'])
            memory_gb = asset.get('hardware_info', {}).get('memory', {}).get('total_gb', 0)
            if memory_gb: all_ram_values_list.append(int(memory_gb))
            c_drive_free = self.get_c_drive_free_space(asset)
            if c_drive_free is not None: all_storage_values_list.append(c_drive_free)
        sorted_os_options, sorted_manufacturer_options = sorted(list(all_os_versions_set)), sorted(list(all_manufacturers_set))
        if not st.session_state.selected_os_filter and sorted_os_options: st.session_state.selected_os_filter = sorted_os_options.copy()
        filters['selected_os'] = st.sidebar.multiselect("OS", sorted_os_options, default=st.session_state.selected_os_filter, key="selected_os_multiselect", on_change=lambda: setattr(st.session_state, 'selected_os_filter', st.session_state.selected_os_multiselect))
        if not st.session_state.selected_manufacturers_filter and sorted_manufacturer_options: st.session_state.selected_manufacturers_filter = sorted_manufacturer_options.copy()
        filters['selected_manufacturers'] = st.sidebar.multiselect("Manufacturer", sorted_manufacturer_options, default=st.session_state.selected_manufacturers_filter, key="selected_manufacturers_multiselect", on_change=lambda: setattr(st.session_state, 'selected_manufacturers_filter', st.session_state.selected_manufacturers_multiselect))
        st.sidebar.subheader("Hardware")
        actual_min_ram, actual_max_ram = (min(all_ram_values_list) if all_ram_values_list else 0), (max(all_ram_values_list) if all_ram_values_list else 128)
        current_ram_filter = st.session_state.ram_range_filter if st.session_state.ram_range_filter else (actual_min_ram, actual_max_ram)
        filters['min_ram'], filters['max_ram'] = st.sidebar.slider("RAM (GB)", actual_min_ram, actual_max_ram, current_ram_filter, key="ram_slider", on_change=lambda: setattr(st.session_state, 'ram_range_filter', st.session_state.ram_slider))
        actual_min_storage, actual_max_storage = 0.0, (max(all_storage_values_list) if all_storage_values_list else 500.0)
        current_storage_filter = st.session_state.storage_range_filter if st.session_state.storage_range_filter else (actual_min_storage, actual_max_storage)
        filters['min_storage'], filters['max_storage'] = st.sidebar.slider("C: Free Space (GB)", actual_min_storage, actual_max_storage, current_storage_filter, key="storage_slider", on_change=lambda: setattr(st.session_state, 'storage_range_filter', st.session_state.storage_slider))
        st.sidebar.subheader("Quick Filters")
        filters['show_low_storage'] = st.sidebar.checkbox("Low Storage (<10GB)", value=st.session_state.show_low_storage_only, key="show_low_storage_checkbox", on_change=lambda: setattr(st.session_state, 'show_low_storage_only', st.session_state.show_low_storage_checkbox))
        filters['anydesk_search'] = st.sidebar.text_input("AnyDesk ID", value=st.session_state.anydesk_search_filter, key="anydesk_search_input", on_change=lambda: setattr(st.session_state, 'anydesk_search_filter', st.session_state.anydesk_search_input))
        filters['search_term'] = st.sidebar.text_input("General Search", value=st.session_state.search_term_filter, key="search_term_input", on_change=lambda: setattr(st.session_state, 'search_term_filter', st.session_state.search_term_input))
        st.sidebar.subheader("Network Scanning")
        scan_type_options = ["Quick Scan", "Full Scan", "Disabled"]
        try: current_scan_type_index = scan_type_options.index(st.session_state.nmap_scan_type)
        except ValueError: current_scan_type_index = 0; st.session_state.nmap_scan_type = "Quick Scan"
        st.sidebar.selectbox("Nmap Scan Type (info only)", scan_type_options, index=current_scan_type_index, key="nmap_scan_type_selector", help="Quick Scan is auto on load. Others for future use.")
        filters['nmap_scan_type'] = st.session_state.nmap_scan_type
        filters['nmap_path'] = st.sidebar.text_input("Nmap Path", value=st.session_state.nmap_path, key="nmap_path_input", on_change=lambda: setattr(st.session_state, 'nmap_path', st.session_state.nmap_path_input))
        with st.sidebar.expander("⚙️ View Customization", expanded=False):
            st.checkbox("Summary & Charts", value=st.session_state.show_summary_section, key="show_summary_cb", on_change=lambda: setattr(st.session_state, 'show_summary_section', st.session_state.show_summary_cb))
            st.checkbox("Asset Bubbles", value=st.session_state.show_bubbles_section, key="show_bubbles_cb", on_change=lambda: setattr(st.session_state, 'show_bubbles_section', st.session_state.show_bubbles_cb))
            st.checkbox("Asset Details Table", value=st.session_state.show_details_table_section, key="show_details_table_cb", on_change=lambda: setattr(st.session_state, 'show_details_table_section', st.session_state.show_details_table_cb))
        return filters

    def filter_assets(self, filters):
        # ... (implementation unchanged) ...
        filtered_assets = {}
        for name, asset in st.session_state.assets_data.items():
            if filters['selected_os'] and self.normalize_os_version(asset.get('os_info', {}).get('version', '')) not in filters['selected_os']: continue
            if filters['selected_manufacturers'] and asset.get('system_info', {}).get('manufacturer', '') not in filters['selected_manufacturers']: continue
            memory_gb = asset.get('hardware_info', {}).get('memory', {}).get('total_gb', 0)
            if memory_gb and (memory_gb < filters['min_ram'] or memory_gb > filters['max_ram']): continue
            c_drive_free = self.get_c_drive_free_space(asset)
            if c_drive_free is not None and (c_drive_free < filters['min_storage'] or c_drive_free > filters['max_storage']): continue
            if filters['show_low_storage'] and (c_drive_free is None or c_drive_free >= 10): continue
            if filters['anydesk_search'] and filters['anydesk_search'].lower() not in asset.get('anydesk_id', '').lower(): continue
            if filters['search_term'] and filters['search_term'].lower() not in json.dumps(asset).lower(): continue
            filtered_assets[name] = asset
        return filtered_assets


    def render_asset_bubbles(self, assets):
        """Render the dense list cards for assets"""
        if not assets: st.warning("No assets match filters."); return
        st.subheader("Asset Inventory List")
        
        # Change to 2 columns for wider, denser cards
        assets_list = list(assets.items()); cols_per_row = 2
        for i in range(0, len(assets_list), cols_per_row):
            cols = st.columns(cols_per_row)
            for j, (name, asset) in enumerate(assets_list[i:i + cols_per_row]):
                with cols[j]: 
                    self.render_single_asset_bubble(name, asset)
    
    def render_single_asset_bubble(self, name, asset):
       # --- Data Extraction ---
       ip_address = asset.get('network_info', {}).get('ip_address', 'No IP')
       os_version = self.normalize_os_version(asset.get('os_info', {}).get('version', 'Unknown OS'))
       memory_gb = asset.get('hardware_info', {}).get('memory', {}).get('total_gb', 0)
       memory_display = f"{int(memory_gb)} GB" if memory_gb else "N/A"
       c_drive_free_gb = self.get_c_drive_free_space(asset)
       c_drive_display = f"{c_drive_free_gb:.1f} GB free" if c_drive_free_gb is not None else "N/A"
       domain = asset.get('pc_domain', 'Unknown Domain')
       uptime = asset.get('os_info', {}).get('uptime', 'N/A')
       winrm_cmd = asset.get('winrm_command', '')

       # --- Status Logic ---
       raw_status = asset.get('network_info', {}).get('status', 'unknown')
       if not isinstance(raw_status, str): raw_status = 'unknown'

       status_for_class = raw_status.lower()
       valid_status_css_classes = ["online", "offline", "scanning", "pending", "failed"]
       if status_for_class not in valid_status_css_classes:
            nmap_scan_status_msg = asset.get('network_info', {}).get('nmap_scan_status', '').lower()
            if "failed" in nmap_scan_status_msg: status_for_class = "failed"
            elif "pending" in nmap_scan_status_msg: status_for_class = "pending"
            elif "skipped" in nmap_scan_status_msg: status_for_class = "scanning"
            else: status_for_class = "unknown"

       status_indicator_class = f"status-indicator-{status_for_class}"
       status_text_class = f"status-{status_for_class}"
       plain_status_text = raw_status.capitalize() if status_for_class in ["online", "offline"] else "Unknown"

       # --- HTML Construction ---
       anydesk_id_val = asset.get('anydesk_id', '')
       anydesk_html = f'<a href="anydesk:{str(anydesk_id_val)}" class="anydesk-link" title="Connect via AnyDesk" target="_blank">Connect ({anydesk_id_val})</a>' if anydesk_id_val and str(anydesk_id_val).strip().lower() != 'n/a' else ''

       # Extract Username smarter
       username = "Unknown User"
       raw_content = asset.get('raw_content', '')
       for line in raw_content.splitlines():
           if "windows account:" in line.lower() or "user account:" in line.lower():
               parts = line.split(':', 1)
               if len(parts) > 1 and parts[1].strip() and parts[1].strip().lower() != "n/a":
                   username = parts[1].strip()
                   break

       # Main Card HTML (Rendered via markdown)
       st.markdown(f"""
       <div class="asset-list-card {status_indicator_class}">
           <div class="status-bar-vertical"></div>
           <div class="card-content-wrapper">
               <div class="card-top-row">
                   <div class="card-title-group">
                       <span class="asset-name">{name}</span>
                       <span class="asset-domain">{domain}</span>
                       <span class="asset-ip">{ip_address}</span>
                   </div>
                   <div class="card-actions-group">
                       <span class="status-text-badge {status_text_class}">{plain_status_text}</span>
                       {anydesk_html}
                   </div>
               </div>
               <div class="card-details-grid">
                   <div class="detail-item" title="{os_version}">🖥️ {os_version}</div>
                   <div class="detail-item">💻 {memory_display} RAM</div>
                   <div class="detail-item">💽 C: {c_drive_display}</div>
                   <div class="detail-item" title="{username}">👤 {username}</div>
                   <div class="detail-item" title="Uptime">⏱️ {uptime}</div>
               </div>
           </div>
       </div>
       """, unsafe_allow_html=True)
       
       # Expander for deep technical details (Rendered natively in Streamlit inside the loop)
       with st.expander("Show Technical Details"):
            col1, col2 = st.columns(2)
            with col1:
                st.markdown("**Hardware & OS**")
                st.markdown(f"- **Model:** {asset.get('system_info', {}).get('model', 'N/A')}")
                st.markdown(f"- **CPU:** {asset.get('hardware_info', {}).get('processor', {}).get('name', 'N/A')}")
                st.markdown(f"- **Install Date:** {asset.get('os_info', {}).get('install_date', 'N/A')}")
                st.markdown(f"- **Reboot Time:** {asset.get('os_info', {}).get('last_reboot', 'N/A')}")
                st.markdown(f"- **BIOS:** {asset.get('system_info', {}).get('bios_version', 'N/A')}")
                st.markdown(f"- **Serial:** {asset.get('system_info', {}).get('serial_number', 'N/A')}")
                
            with col2:
                st.markdown("**Network & Security**")
                st.markdown(f"- **Gateway:** {asset.get('network_info', {}).get('default_gateway', 'N/A')}")
                st.markdown(f"- **DNS:** {asset.get('network_info', {}).get('dns_servers', 'N/A')}")
                st.markdown(f"- **Antivirus:** {asset.get('software_info', {}).get('antivirus', 'N/A')}")
                
                # Bitlocker logic
                bl_status = asset.get('bitlocker_status', [])
                bl_text = ", ".join(bl_status) if bl_status else "Unknown"
                st.markdown(f"- **Bitlocker:** {bl_text}")

            if winrm_cmd:
                st.markdown("**Quick WinRM**")
                st.code(winrm_cmd, language="powershell")

    def render_status_distribution_chart(self, assets):
        # ... (implementation unchanged) ...
        if not assets: return
        st.subheader("Assets by Status"); status_counts = {}
        for asset_data in assets.values(): status = asset_data.get('network_info', {}).get('status', 'unknown'); status_counts[status] = status_counts.get(status, 0) + 1
        if status_counts:
            fig = px.pie(values=list(status_counts.values()), names=list(status_counts.keys()), title="Asset Status Overview")
            fig.update_traces(textposition='inside', textinfo='percent+label'); st.plotly_chart(fig, use_container_width=True)
        else: st.info("No status data for visualization.")

    def render_asset_details_modal(self, assets):
        # ... (implementation unchanged) ...
        pass # Placeholder for brevity

    def render_overview_metrics(self, assets):
        """Render overview metrics cards"""
        if not assets:
            st.warning("No assets match the current filters.")
            return

        st.markdown('<div class="metric-card">', unsafe_allow_html=True)
        st.subheader("Dashboard Overview")
        col1, col2, col3, col4 = st.columns(4)

        # Calculate metrics
        total_assets = len(assets)
        online_assets = sum(1 for asset in assets.values()
                          if asset.get('network_info', {}).get('status') == 'online')

        ram_total = 0
        storage_total = 0

        for asset in assets.values():
            # RAM total (convert to GB if needed)
            ram_info = asset.get('hardware_info', {}).get('memory', {})
            if isinstance(ram_info, dict) and 'total_gb' in ram_info:
                ram_total += ram_info['total_gb']

            # Storage total
            storage_info = asset.get('hardware_info', {}).get('storage', [])
            if isinstance(storage_info, list):
                for drive in storage_info:
                    if isinstance(drive, dict) and 'size_gb' in drive:
                        storage_total += drive['size_gb']

        with col1:
            st.metric("Total Assets Managed", total_assets)

        with col2:
             # Calculate percentage for better context
            online_pct = (online_assets / total_assets) * 100 if total_assets > 0 else 0
            st.metric("Online Assets", online_assets, delta=f"{online_pct:.1f}% Active", delta_color="normal")

        with col3:
            st.metric("Total Configured RAM", f"{ram_total:.0f} GB" if ram_total > 0 else "N/A")

        with col4:
            st.metric("Total Provisioned Storage", f"{storage_total:.0f} GB" if storage_total > 0 else "N/A")
            
        st.markdown('</div>', unsafe_allow_html=True)

    def render_asset_details(self, assets):
        """Render detailed asset information in a table"""
        logger.info(f"render_asset_details: Received assets. Count: {len(assets) if assets else 'None or empty'}")

        if not assets:
            logger.warning("render_asset_details: No assets data provided or assets are empty.")
            st.warning("No asset data available to display details.")
            return

        st.subheader("Asset Details")

        table_data = []
        try:
            logger.info("render_asset_details: Starting preparation of table_data.")
            for name, asset in assets.items():
                try:
                    row = {
                        'Computer Name': name,
                        'IP Address': asset.get('network_info', {}).get('ip_address', 'N/A'),
                        'OS': asset.get('os_info', {}).get('version', 'N/A'),
                        'Manufacturer': asset.get('system_info', {}).get('manufacturer', 'N/A'),
                        'Model': asset.get('system_info', {}).get('model', 'N/A'),
                        'RAM (GB)': asset.get('hardware_info', {}).get('memory', {}).get('total_gb', 'N/A'),
                        'CPU': asset.get('hardware_info', {}).get('processor', {}).get('name', 'N/A'),
                        'Status': asset.get('network_info', {}).get('status', 'Unknown')
                    }
                    table_data.append(row)
                except Exception as e:
                    logger.error(f"render_asset_details: Error processing asset '{name}': {str(e)}")
                    # Optionally, add a placeholder row or skip
            logger.info(f"render_asset_details: table_data preparation complete. Number of rows: {len(table_data)}")
            if not table_data:
                logger.warning("render_asset_details: table_data is empty after processing assets.")
                st.info("No data could be prepared for the asset details table.")
                return
        except Exception as e:
            logger.error(f"render_asset_details: Error during table_data preparation loop: {str(e)}")
            st.error("An error occurred while preparing asset data for display.")
            return

        try:
            logger.info("render_asset_details: Creating DataFrame from table_data.")
            df = pd.DataFrame(table_data)
            logger.info(f"render_asset_details: DataFrame created. Shape: {df.shape}. Head: {df.head().to_string() if not df.empty else 'Empty DataFrame'}")
        except Exception as e:
            logger.error(f"render_asset_details: Failed to create DataFrame: {str(e)}")
            st.error("Failed to create the data table for asset details.")
            return

        if not df.empty:
            try:
                logger.info("render_asset_details: Converting DataFrame to CSV.")
                csv = df.to_csv(index=False)
                logger.info("render_asset_details: CSV conversion successful.")

                logger.info("render_asset_details: Preparing download button.")
                st.download_button(
                    label="📥 Download Asset Report (CSV)",
                    data=csv,
                    file_name=f"asset_report_{datetime.now().strftime('%Y%m%d_%H%M%S')}.csv",
                    mime="text/csv",
                    key="download_asset_report_csv" # Added a key for robustness
                )
                logger.info("render_asset_details: Download button prepared.")
            except Exception as e:
                logger.error(f"render_asset_details: Failed to convert DataFrame to CSV or prepare download button: {str(e)}")
                # Still display the table if CSV fails
        else:
            logger.info("render_asset_details: DataFrame is empty, skipping CSV conversion and download button.")
            st.info("No data available in the table to download as CSV.") # Inform user

        try:
            logger.info("render_asset_details: Displaying DataFrame.")
            st.dataframe(df, use_container_width=True)
            logger.info("render_asset_details: DataFrame displayed successfully.")
        except Exception as e:
            logger.error(f"render_asset_details: Failed to display DataFrame: {str(e)}")
            st.error("Failed to display the asset details table.")

    def render_system_statistics(self, assets):
        """Render system statistics with pie charts"""
        st.subheader("System Statistics")

        col1, col2 = st.columns(2)

        with col1:
            # OS Distribution
            os_data = {}
            for asset in assets.values():
                os_version = self.normalize_os_version(asset.get('os_info', {}).get('version', 'Unknown'))
                os_data[os_version] = os_data.get(os_version, 0) + 1

            if os_data:
                fig_os = px.pie(
                    values=list(os_data.values()),
                    names=list(os_data.keys()),
                    title="Operating System Distribution",
                    color_discrete_sequence=px.colors.qualitative.Set3
                )
                fig_os.update_traces(textposition='inside', textinfo='percent+label')
                st.plotly_chart(fig_os, use_container_width=True)

        with col2:
            # Manufacturer Distribution
            manufacturer_data = {}
            for asset in assets.values():
                manufacturer = asset.get('system_info', {}).get('manufacturer', 'Unknown')
                manufacturer_data[manufacturer] = manufacturer_data.get(manufacturer, 0) + 1

            if manufacturer_data:
                fig_mfg = px.pie(
                    values=list(manufacturer_data.values()),
                    names=list(manufacturer_data.keys()),
                    title="System Manufacturer Distribution",
                    color_discrete_sequence=px.colors.qualitative.Set2
                )
                fig_mfg.update_traces(textposition='inside', textinfo='percent+label')
                st.plotly_chart(fig_mfg, use_container_width=True)

    def run(self):
        """Main application entry point"""
        try:
            if 'view_asset' in st.query_params:
                try:
                    if not st.session_state.assets_data:
                        with st.spinner("Loading asset data..."):
                             st.session_state.assets_data = self.load_assets_data()
                    asset_name_from_query = urllib.parse.unquote(st.query_params['view_asset'])
                    if asset_name_from_query and asset_name_from_query in st.session_state.assets_data:
                        st.session_state.selected_asset_for_details = asset_name_from_query
                        st.session_state.show_asset_details = True
                    elif asset_name_from_query:
                        st.warning(f"Asset '{asset_name_from_query}' specified in URL not found.")
                    st.query_params.clear()
                except Exception as e:
                    logger.error(f"Error processing view_asset query param: {e}", exc_info=True)
                    st.error("Failed to process asset view request from URL.")
                    if 'view_asset' in st.query_params:
                        try: st.query_params.clear()
                        except Exception as e_clear: logger.error(f"Failed to clear query_params on error: {e_clear}")
            
            self.check_and_install_dependencies()
            apply_windows11_theme()
            
            if not st.session_state.assets_data or 'refresh_trigger' in st.session_state:
                if 'refresh_trigger' in st.session_state: del st.session_state['refresh_trigger']
                logger.info("No Nmap queue to reset.") # Nmap queue was removed
                with st.spinner("Loading asset data (including Nmap Quick Scans)..."):
                    st.session_state.assets_data = self.load_assets_data()

            self.render_header()
            filters = self.render_sidebar_filters() # This now returns a dict of actual filter values

            # This block for active pills display logic is kept from previous state,
            # ensure it correctly uses session state for filter values.
            if st.session_state.assets_data and filters:
                all_os_versions_set, all_manufacturers_set, all_ram_values_list, all_storage_values_list = set(), set(), [], []
                for asset in st.session_state.assets_data.values():
                    if asset.get('os_info', {}).get('version'): all_os_versions_set.add(self.normalize_os_version(asset['os_info']['version']))
                    if asset.get('system_info', {}).get('manufacturer'): all_manufacturers_set.add(asset['system_info']['manufacturer'])
                    memory_gb = asset.get('hardware_info', {}).get('memory', {}).get('total_gb', 0)
                    if memory_gb: all_ram_values_list.append(int(memory_gb))
                    c_drive_free = self.get_c_drive_free_space(asset)
                    if c_drive_free is not None: all_storage_values_list.append(c_drive_free)

                default_min_ram = min(all_ram_values_list) if all_ram_values_list else 0
                default_max_ram = max(all_ram_values_list) if all_ram_values_list else 128
                default_min_storage = 0.0
                default_max_storage = max(all_storage_values_list) if all_storage_values_list else 500.0

                active_pills_data = []
                if len(st.session_state.selected_os_filter) != len(all_os_versions_set):
                    for os_name in st.session_state.selected_os_filter: active_pills_data.append((f"OS: {os_name}", f"dismiss_os_{os_name}", {"type": "os", "value": os_name}))
                if len(st.session_state.selected_manufacturers_filter) != len(all_manufacturers_set):
                    for manuf_name in st.session_state.selected_manufacturers_filter: active_pills_data.append((f"Manuf: {manuf_name}", f"dismiss_manuf_{manuf_name}", {"type": "manufacturer", "value": manuf_name}))
                current_ram_filter = st.session_state.ram_range_filter
                if current_ram_filter and (current_ram_filter[0] != default_min_ram or current_ram_filter[1] != default_max_ram): active_pills_data.append((f"RAM: {current_ram_filter[0]}-{current_ram_filter[1]} GB", "dismiss_ram", {"type": "ram_range"}))
                current_storage_filter = st.session_state.storage_range_filter
                if current_storage_filter and (current_storage_filter[0] != default_min_storage or current_storage_filter[1] != default_max_storage): active_pills_data.append((f"Storage: {current_storage_filter[0]:.1f}-{current_storage_filter[1]:.1f} GB", "dismiss_storage", {"type": "storage_range"}))
                if st.session_state.show_low_storage_only: active_pills_data.append(("Status: Low Storage", "dismiss_low_storage", {"type": "show_low_storage"}))
                if st.session_state.anydesk_search_filter: active_pills_data.append((f"AnyDesk: {st.session_state.anydesk_search_filter}", "dismiss_anydesk", {"type": "anydesk_search"}))
                if st.session_state.search_term_filter: active_pills_data.append((f"Search: \"{st.session_state.search_term_filter}\"", "dismiss_search", {"type": "search_term"}))

                if active_pills_data:
                    st.markdown('<div class="filter-pill-container">', unsafe_allow_html=True)
                    pills_per_row_approx = 4
                    num_rows = (len(active_pills_data) + pills_per_row_approx - 1) // pills_per_row_approx
                    for i in range(num_rows):
                        cols = st.columns(pills_per_row_approx)
                        for j in range(pills_per_row_approx):
                            pill_index = i * pills_per_row_approx + j
                            if pill_index < len(active_pills_data):
                                pill_text, dismiss_key, action_args = active_pills_data[pill_index]
                                with cols[j]:
                                    st.markdown(f'<div class="filter-pill"><span>{pill_text}</span>', unsafe_allow_html=True)
                                    if st.button("×", key=dismiss_key, help=f"Remove {pill_text} filter"):
                                        filter_type = action_args["type"]
                                        if filter_type == "os": st.session_state.selected_os_filter.remove(action_args["value"]); st.session_state.selected_os_filter = st.session_state.selected_os_filter or (sorted(list(all_os_versions_set)) if all_os_versions_set else [])
                                        elif filter_type == "manufacturer": st.session_state.selected_manufacturers_filter.remove(action_args["value"]); st.session_state.selected_manufacturers_filter = st.session_state.selected_manufacturers_filter or (sorted(list(all_manufacturers_set)) if all_manufacturers_set else [])
                                        elif filter_type == "ram_range": st.session_state.ram_range_filter = None
                                        elif filter_type == "storage_range": st.session_state.storage_range_filter = None
                                        elif filter_type == "show_low_storage": st.session_state.show_low_storage_only = False
                                        elif filter_type == "anydesk_search": st.session_state.anydesk_search_filter = ""
                                        elif filter_type == "search_term": st.session_state.search_term_filter = ""
                                        st.rerun()
                                    st.markdown('</div>', unsafe_allow_html=True)
                    st.markdown('</div>', unsafe_allow_html=True)

            # Use the 'filters' dict returned by render_sidebar_filters for filtering logic
            # This dict should reflect the latest state from session_state due to on_change callbacks
            filtered_assets = self.filter_assets(filters)
            self.render_asset_details_modal(filtered_assets)

            if st.session_state.get('show_summary_section', True):
                st.markdown('<div class="summary-charts-container">', unsafe_allow_html=True)
                self.render_overview_metrics(filtered_assets)
                st.divider()
                self.render_system_statistics(filtered_assets)
                self.render_status_distribution_chart(filtered_assets)
                st.markdown('</div>', unsafe_allow_html=True)
                st.divider()

            if filtered_assets:
                if st.session_state.get('show_bubbles_section', True):
                    self.render_asset_bubbles(filtered_assets)
                    if st.session_state.get('show_details_table_section', True): st.divider()
                if st.session_state.get('show_details_table_section', True):
                    self.render_asset_details(filtered_assets)
            else:
                if st.session_state.assets_data: st.warning("No assets match filters.")
                else: st.info("Welcome! Place asset files in 'assets' and refresh.")
        except Exception as e:
            logger.error(f"Application error: {str(e)}", exc_info=True)
            st.error(f"An unhandled error occurred: {str(e)}")

if __name__ == "__main__":
    app = ITAssetDashboard()
    app.run()