import os
import sys
import subprocess
import re
import glob
import json
from pathlib import Path
import functools  # Added for functools.partial

# We'll import third-party libraries after ensuring they are installed


# Function to check and install dependencies
def check_and_install_dependencies():
    required_packages = [
        "pandas",
        "gradio",
        "matplotlib",
        "plotly"
    ]
    
    # Create a flag file to track if dependencies have been installed
    flag_file = Path("dependencies_installed.flag")

    # Check if required packages can be imported
    missing_packages = []
    for package in required_packages:
        try:
            __import__(package)
        except ImportError:
            missing_packages.append(package)

    if missing_packages or not flag_file.exists():
        print("Installing missing dependencies...")
        try:
            subprocess.check_call([sys.executable, "-m", "pip", "install", "--upgrade", "pip"])
            for package in missing_packages:
                print(f"Installing {package}...")
                subprocess.check_call([sys.executable, "-m", "pip", "install", package])
            flag_file.touch()
            print("All dependencies installed successfully!")
        except Exception as e:
            print(f"Error installing dependencies: {e}")
            sys.exit(1)
    else:
        print("Dependencies already installed.")

# Call the function to check and install dependencies before importing third-party libraries
check_and_install_dependencies()

# Now that dependencies are ensured, import them
import pandas as pd
import gradio as gr
import plotly.express as px
import plotly.graph_objects as go
import matplotlib.pyplot as plt

# Function to parse asset information from text files
def parse_asset_files():
    assets_folder = os.path.join(os.path.dirname(__file__), "attached_assets")
    asset_files = glob.glob(os.path.join(assets_folder, "*.txt"))
    
    all_assets = []
    
    for file_path in asset_files:
        try:
            with open(file_path, 'r', encoding='utf-8-sig') as file:
                content = file.read()
                
            # Extract filename to get IP and hostname
            filename = os.path.basename(file_path)
            ip_hostname = filename.replace(".txt", "").split("_")
            ip = ip_hostname[0] if len(ip_hostname) > 0 else ""
            hostname = ip_hostname[1] if len(ip_hostname) > 1 else ""
            
            # Extract key information using regex
            asset_info = {
                "IP": ip,
                "Hostname": hostname,
                "File": filename
            }
            
            # Extract common fields
            patterns = {
                "OS Version": r"OS Version:\s*(.*?)(?:\n|$)",
                "User Email": r"User Email\(s\):\s*(.*?)(?:\n|$)",
                "CPU": r"CPU:\s*(.*?)(?:\n|$)",
                "RAM": r"RAM:\s*(.*?)(?:\n|$)",
                "GPU": r"GPU:\s*(.*?)(?:\n|$)",
                "System Manufacturer": r"System Manufacturer:\s*(.*?)(?:\n|$)",
                "System Model": r"System Model:\s*(.*?)(?:\n|$)",
                "BIOS Version": r"BIOS Version:\s*(.*?)(?:\n|$)",
                "Windows Language": r"Windows Language:\s*(.*?)(?:\n|$)",
                "Antivirus": r"Antivirus:\s*(.*?)(?:\n|$)",
                "Office Version": r"Office Version:\s*(.*?)(?:\n|$)",
                "OS Activation": r"OS Activation:\s*(.*?)(?:\n|$)",
                "AnyDesk ID": r"AnyDesk ID:\s*(\d+)(?:\n|$)"
            }
            
            for key, pattern in patterns.items():
                match = re.search(pattern, content)
                # Modify asset_info population loop in parse_asset_files
                asset_info[key] = match.group(1).strip() if match and match.group(1) else "N/A"

            # Extract disk information
            disk_section = re.search(r"=== Local Disks \(in MB\) ===(.*?)(?:===|$)", content, re.DOTALL)
            if disk_section:
                disk_info = disk_section.group(1).strip()
                asset_info["Disk Info"] = disk_info

                # Extract C drive free space
                c_drive_match = re.search(r"C:.*?Free: ([\d.]+) MB", disk_info)
                if c_drive_match:
                    asset_info["C Drive Free Space (MB)"] = float(c_drive_match.group(1))
                    asset_info["C Drive Free Space (GB)"] = round(float(c_drive_match.group(1)) / 1024, 2)
                else:
                    asset_info["C Drive Free Space (MB)"] = "N/A"
                    asset_info["C Drive Free Space (GB)"] = "N/A"
            else:
                asset_info["Disk Info"] = "N/A"
                asset_info["C Drive Free Space (MB)"] = "N/A"
                asset_info["C Drive Free Space (GB)"] = "N/A"


            all_assets.append(asset_info)
        except Exception as e:
            print(f"Error parsing file {file_path}: {e}")

    return all_assets

# Load asset data
assets_data = parse_asset_files()
assets_df = pd.DataFrame(assets_data)

# Sanitize data after parsing
assets_df = assets_df.fillna("N/A")  # Replace NaN with "N/A"
# Convert everything to string for JSON compatibility, except for known numeric columns
for col in assets_df.columns:
    if col not in ["C Drive Free Space (MB)", "C Drive Free Space (GB)"]:
        assets_df[col] = assets_df[col].astype(str)


# Add a function to normalize OS versions
def normalize_os_version(os_string):
    if os_string == "N/A":
        return "Unknown OS"
    
    os_string = os_string.strip()
    
    # Extract the main OS name from the full string
    # Handle common patterns in the data files
    if "Windows 10" in os_string:
        return "Windows 10"
    elif "Windows 11" in os_string:
        return "Windows 11"
    elif "Windows 8.1" in os_string or "6.3.9600" in os_string:
        return "Windows 8.1"
    elif "Windows 7" in os_string or "6.1.7601" in os_string:
        return "Windows 7"
    elif "Windows Server" in os_string:
        # Extract the server version if available
        server_version_match = re.search(r"Windows Server (\d+)", os_string)
        if server_version_match:
            return f"Windows Server {server_version_match.group(1)}"
        return "Windows Server"
    elif "Linux" in os_string:
        # Try to extract the distribution name
        distro_match = re.search(r"Linux\s+(\w+)", os_string)
        if distro_match:
            return f"Linux {distro_match.group(1)}"
        return "Linux"
    elif "macOS" in os_string or "Mac OS" in os_string:
        # Try to extract the macOS version
        macos_version_match = re.search(r"(macOS|Mac OS)\s+([\d\.]+)", os_string)
        if macos_version_match:
            return f"macOS {macos_version_match.group(2)}"
        return "macOS"
    
    # If no specific rule matches, try to extract the main OS name
    # This handles cases like "Microsoft Windows 10 Pro (10.0.19045 Build 19045)"
    os_name_match = re.search(r"Microsoft\s+(Windows\s+\w+)", os_string)
    if os_name_match:
        return os_name_match.group(1)
    
    # Return original if no specific rule matches
    return os_string

# Apply normalization after creating the DataFrame
assets_df['Normalized OS'] = assets_df['OS Version'].apply(normalize_os_version)

# Get unique normalized OS values for buttons
unique_normalized_oss = assets_df['Normalized OS'].dropna().unique().tolist()
unique_normalized_oss.sort() # Sort for consistent order

# Dashboard functions
def filter_assets(hostname_filter, os_filter, manufacturer_filter, min_ram, max_ram, filter_low_storage=False, page_index=0, page_size=None):
    filtered_df = assets_df.copy()
    
    # Print debug information
    print(f"Filtering with OS filter: '{os_filter}' (Type: {type(os_filter)})") 
    print(f"Available OS values: {filtered_df['Normalized OS'].unique().tolist()}")
    
    if hostname_filter:
        filtered_df = filtered_df[filtered_df['Hostname'].str.contains(hostname_filter, case=False, na=False)]
    
    # Use 'Normalized OS' for filtering if os_filter is provided and is a non-empty string
    if os_filter and isinstance(os_filter, str) and os_filter.strip():
        filtered_df = filtered_df[filtered_df['Normalized OS'].str.contains(os_filter, case=False, na=False)]
        print(f"After OS filtering, found {len(filtered_df)} results")
    
    if manufacturer_filter:
        filtered_df = filtered_df[filtered_df['System Manufacturer'].str.contains(manufacturer_filter, case=False, na=False)]
    
    # Extract numeric RAM values for filtering
    filtered_df['RAM_Value'] = filtered_df['RAM'].str.extract(r'(\d+(?:\,\d+)?)')
    filtered_df['RAM_Value'] = filtered_df['RAM_Value'].replace(',', '.', regex=True).astype(float)
    
    if min_ram is not None:
        filtered_df = filtered_df[filtered_df['RAM_Value'] >= min_ram]
    
    if max_ram is not None:
        filtered_df = filtered_df[filtered_df['RAM_Value'] <= max_ram]
    
    # Drop the temporary column
    filtered_df = filtered_df.drop('RAM_Value', axis=1)

    # Filter by low storage if requested
    if filter_low_storage:
        numeric_disk_space = pd.to_numeric(filtered_df['C Drive Free Space (GB)'], errors='coerce')
        filtered_df = filtered_df[numeric_disk_space < 10]

    # Display only relevant columns in the results table
    # For bubble display, we need more columns than just for a table.
    # Ensure 'AnyDesk ID' is available for bubbles.
    bubble_display_columns = ['IP', 'Hostname', 'Normalized OS', 'CPU', 'RAM', 'System Manufacturer', 'C Drive Free Space (GB)', 'AnyDesk ID']
    # Ensure all requested columns actually exist in filtered_df
    bubble_display_columns = [col for col in bubble_display_columns if col in filtered_df.columns]


    # --- Display Logic ---
    total_items = len(filtered_df)
    
    if page_size is None:
        # When displaying all, select necessary columns for bubbles from filtered_df
        display_df = filtered_df[bubble_display_columns].copy()
        total_pages = 1
        page_index = 0
    else:
        # When paginating, select necessary columns for bubbles from the paginated slice of filtered_df
        total_pages = (total_items + page_size - 1) // page_size 
        start_index = page_index * page_size
        end_index = min(start_index + page_size, total_items)
        # Ensure to select bubble_display_columns from the main filtered_df before slicing for pagination,
        # or select from the slice if the slice is already a DataFrame.
        # Let's assume filtered_df.iloc[start_index:end_index] gives a DataFrame, then select columns.
        display_df = filtered_df.iloc[start_index:end_index][bubble_display_columns].copy()
    
    numeric_cols_to_convert = ["C Drive Free Space (GB)"]
    for col in numeric_cols_to_convert:
        if col in display_df.columns:
            display_df[col] = display_df[col].astype(str)
    
    # --- End Display Logic ---

    html_content = """
    <style>
    .asset-grid-container {
        display: flex;
        flex-wrap: wrap;
        gap: 15px;
        justify-content: flex-start;
        margin-bottom: 20px;
    }
    .asset-bubble {
        background: linear-gradient(135deg, #6e8efb, #a777e3);
        border-radius: 12px;
        color: white;
        padding: 15px;
        min-width: 200px;
        max-width: 300px;
        flex: 1;
        box-shadow: 0 4px 8px rgba(0,0,0,0.1);
        transition: all 0.3s ease;
        text-align: center;
        display: flex; 
        flex-direction: column; 
        justify-content: space-between; 
    }
    .asset-bubble:hover {
        transform: translateY(-5px);
        box-shadow: 0 6px 12px rgba(0,0,0,0.15);
    }
    .asset-bubble.windows10 { background: linear-gradient(135deg, #00a2ed, #0078d7); }
    .asset-bubble.windows11 { background: linear-gradient(135deg, #0078d7, #0063b1); }
    .asset-bubble.windows8 { background: linear-gradient(135deg, #00b2f0, #0072c6); }
    .asset-bubble.windows7 { background: linear-gradient(135deg, #6a737b, #36454f); }
    .asset-bubble.linux { background: linear-gradient(135deg, #f57c00, #d84315); }
    .asset-bubble.macos { background: linear-gradient(135deg, #8e8e93, #636366); }
    .asset-bubble.unknown { background: linear-gradient(135deg, #9e9e9e, #616161); }
    .asset-hostname { font-size: 18px; font-weight: bold; margin-bottom: 5px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .asset-ip { font-size: 14px; opacity: 0.9; }
    .asset-os { font-size: 12px; margin-top: 8px; opacity: 0.8; background-color: rgba(255, 255, 255, 0.2); border-radius: 10px; padding: 3px 8px; display: inline-block; }
    .asset-ram { font-size: 12px; margin-top: 5px; opacity: 0.8; }
    .asset-disk-space { font-size: 12px; margin-top: 5px; opacity: 0.8; }
    .asset-bubble.low-storage-alert { border: 3px solid #FF6347; }
    .low-storage-warning-text { font-weight: bold; color: #FF6347; font-size: 12px; margin-top: 5px; }
    .view-details-btn { background-color: rgba(255, 255, 255, 0.3); color: white; border: none; padding: 8px 12px; border-radius: 6px; cursor: pointer; margin-top: 10px; transition: background-color 0.2s ease; }
    .view-details-btn:hover { background-color: rgba(255, 255, 255, 0.5); }
    .pagination-info { text-align: center; margin: 10px 0; font-weight: bold; }
    @media (max-width: 768px) { .asset-bubble { min-width: 150px; } }
    </style>
    <div class="asset-grid-container">
    """
    
    if not display_df.empty:
        for _, row in display_df.iterrows():
            hostname = row.get('Hostname', 'Unknown')
            hostname_escaped = json.dumps(hostname)[1:-1] 
            ip = row.get('IP', 'N/A')
            os_version = row.get('Normalized OS', 'Unknown OS')
            ram = row.get('RAM', 'N/A')
            free_space_gb_raw = row.get('C Drive Free Space (GB)', 'N/A')
            anydesk_id = row.get('AnyDesk ID', 'N/A')
            anydesk_html = ""
            if anydesk_id != "N/A" and anydesk_id.isdigit() and anydesk_id.strip(): # Check isdigit and not just whitespace
                anydesk_html = f'<a href="anydesk:{anydesk_id}" target="_blank" style="font-size: 11px; color: white; text-decoration: underline; display: block; margin-top: 5px;">Anydesk: {anydesk_id}</a>'
            else:
                anydesk_html = '<div style="font-size: 11px; color: white; margin-top: 5px; opacity: 0.8;">Anydesk: N/A</div>'
            
            css_class = "unknown"
            if "Windows 10" in os_version: css_class = "windows10"
            elif "Windows 11" in os_version: css_class = "windows11"
            elif "Windows 8" in os_version: css_class = "windows8"
            elif "Windows 7" in os_version: css_class = "windows7"
            elif "Linux" in os_version: css_class = "linux"
            elif "macOS" in os_version: css_class = "macos"
            low_storage_alert_class = ""
            low_storage_warning_html = ""
            try:
                free_space_gb_val = float(free_space_gb_raw)
                if free_space_gb_val < 10:
                    low_storage_alert_class = "low-storage-alert"
                    low_storage_warning_html = '<div class="low-storage-warning-text">LOW STORAGE!</div>'
            except ValueError:
                pass 
            html_content += f"""
            <div class="asset-bubble {css_class} {low_storage_alert_class}"> 
                <div> <!-- Added a div to wrap content other than button for flex layout -->
                    <div class="asset-hostname">{hostname}</div>
                    <div class="asset-ip">{ip}</div>
                    <div class="asset-os">{os_version}</div>
                    <div class="asset-ram">{ram}</div>
                    <div class="asset-disk-space">Disk: {free_space_gb_raw} GB</div>
                    {anydesk_html} 
                    {low_storage_warning_html}
                </div>
                <button class='view-details-btn' onclick='js_trigger_py_modal(\"{hostname_escaped}\")'>View Details</button>
            </div>
            """
    else:
        html_content += """
        <div style="text-align: center; width: 100%; padding: 20px;">
            <p>No assets found matching the current filters.</p>
        </div>
        """
    html_content += "</div>"
    html_content += f"""
    <div class="pagination-info">
        Showing {total_items} assets
    </div>
    """
    js_script = """
    <script>
    function js_trigger_py_modal(hostname) {
        const hostnameInput = document.getElementById('py_modal_trigger_hostname_input');
        if (hostnameInput) {
            hostnameInput.value = hostname;
            const inputEvent = new Event('input', { bubbles: true });
            hostnameInput.dispatchEvent(inputEvent);
            const changeEvent = new Event('change', { bubbles: true });
            hostnameInput.dispatchEvent(changeEvent);
        } else {
            console.error('py_modal_trigger_hostname_input not found');
        }
    }
    </script>
    """
    if page_size is None:
        return html_content + js_script, f"Showing all {total_items} assets"
    else:
        return html_content + js_script, str(f"Page {page_index + 1} of {total_pages}")

initial_table, initial_total_pages = filter_assets("", "", "", None, None, page_size=None)

def get_asset_details(hostname):
    if not hostname:
        return """<div class="asset-details-placeholder">
            <h3>Select a hostname to view asset details</h3>
            <p>Click on a hostname in the results table or select one from the dropdown.</p>
        </div>"""
    selected_asset = assets_df[assets_df['Hostname'] == hostname]
    if selected_asset.empty:
        return f"""<div class="asset-details-error">
            <h3>Error</h3>
            <p>No asset found with the hostname: {hostname}</p>
        </div>"""
    try:
        asset_dict = selected_asset.iloc[0].to_dict()
        for k, v in asset_dict.items():
            if pd.isna(v): asset_dict[k] = "N/A"
            elif not isinstance(v, str): asset_dict[k] = str(v)
        
        # Prepare AnyDesk ID HTML
        anydesk_id = asset_dict.get('AnyDesk ID', 'N/A')
        anydesk_html_value = "N/A"
        if anydesk_id != "N/A" and anydesk_id.isdigit():
            anydesk_html_value = f'<a href="anydesk:{anydesk_id}">{anydesk_id}</a>'
        else:
            anydesk_html_value = anydesk_id # Display "N/A" or other non-digit values as is

        html = f"""
        <style>
        .asset-dashboard {{ background: white; border-radius: 12px; box-shadow: 0 8px 30px rgba(0,0,0,0.2); padding: 25px; max-width: 900px; margin: 0 auto; position: relative; }}
        .asset-header {{ border-bottom: 2px solid #f0f0f0; padding-bottom: 15px; margin-bottom: 20px; display: flex; justify-content: space-between; align-items: center; }}
        .asset-header h2 {{ margin: 0; color: #2c3e50; font-size: 24px; }}
        .asset-header-right {{ text-align: right; }}
        .asset-section {{ margin-bottom: 25px; }}
        .asset-section h3 {{ color: #3498db; border-left: 4px solid #3498db; padding-left: 10px; margin-bottom: 15px; }}
        .asset-grid {{ display: grid; grid-template-columns: repeat(auto-fill, minmax(250px, 1fr)); gap: 15px; }}
        .asset-item {{ background: #f8f9fa; padding: 12px; border-radius: 8px; }}
        .asset-item strong {{ color: #34495e; }}
        .user-accounts-section {{ margin-top: 20px; }}
        .software-list {{ max-height: 300px; overflow-y: auto; border: 1px solid #eee; padding: 10px; border-radius: 8px; }}
        </style>
        <div class="asset-dashboard">
            <div class="asset-header">
                <div><h2>{asset_dict.get('Hostname', 'N/A')}</h2><p>IP: {asset_dict.get('IP', 'N/A')}</p></div>
                <div class="asset-header-right"><p><strong>Last Seen:</strong> {asset_dict.get('Last Seen', 'N/A')}</p><p><strong>Status:</strong> <span style="color: green;">Active</span></p></div>
            </div>
            <div class="asset-section"><h3>System Information</h3><div class="asset-grid">
                <div class="asset-item"><strong>OS:</strong> {asset_dict.get('Normalized OS', 'N/A')}</div>
                <div class="asset-item"><strong>CPU:</strong> {asset_dict.get('CPU', 'N/A')}</div>
                <div class="asset-item"><strong>RAM:</strong> {asset_dict.get('RAM', 'N/A')}</div>
                <div class="asset-item"><strong>GPU:</strong> {asset_dict.get('GPU', 'N/A')}</div>
                <div class="asset-item"><strong>Manufacturer:</strong> {asset_dict.get('System Manufacturer', 'N/A')}</div>
                <div class="asset-item"><strong>Model:</strong> {asset_dict.get('System Model', 'N/A')}</div>
            </div></div>
            <div class="asset-section"><h3>Storage</h3><div class="asset-grid">
                <div class="asset-item"><strong>C Drive Free Space:</strong> {asset_dict.get('C Drive Free Space (GB)', 'N/A')} GB</div>
                <div class="asset-item"><strong>Disk Info:</strong> <pre>{asset_dict.get('Disk Info', 'N/A')}</pre></div>
            </div></div>
            <div class="asset-section"><h3>Software</h3><div class="asset-grid">
                <div class="asset-item"><strong>Windows Language:</strong> {asset_dict.get('Windows Language', 'N/A')}</div>
                <div class="asset-item"><strong>Antivirus:</strong> {asset_dict.get('Antivirus', 'N/A')}</div>
                <div class="asset-item"><strong>Office Version:</strong> {asset_dict.get('Office Version', 'N/A')}</div>
                <div class="asset-item"><strong>OS Activation:</strong> {asset_dict.get('OS Activation', 'N/A')}</div>
            </div></div>
            <div class="asset-section"><h3>User Accounts</h3><div class="asset-grid">
                <div class="asset-item"><strong>Current User:</strong> {asset_dict.get('Current User', 'N/A')}</div>
                <div class="asset-item"><strong>Domain:</strong> {asset_dict.get('Domain', 'N/A')}</div>
            </div><div class="user-accounts-section"><h4>User Email Accounts</h4><div class="asset-item"><ul>
                <li><strong>Primary Email:</strong> user@company.com</li><li><strong>Secondary Email:</strong> user.backup@company.com</li>
            </ul></div></div></div>
            <div class="asset-section"><h3>Installed Software</h3><div class="software-list"><ul>
                <li>Microsoft Office 365</li><li>Google Chrome</li><li>Mozilla Firefox</li><li>Adobe Acrobat Reader</li><li>7-Zip</li>
                <li>VLC Media Player</li><li>Microsoft Teams</li><li>Zoom</li><li>Slack</li><li>Notepad++</li>
            </ul></div></div>
            <div class="asset-section"><h3>Network</h3><div class="asset-grid">
                <div class="asset-item"><strong>PC Domain:</strong> {asset_dict.get('PC Domain', 'N/A')}</div>
                <div class="asset-item"><strong>AnyDesk ID:</strong> {anydesk_html_value}</div>
                <div class="asset-item"><strong>Windows Account:</strong> {asset_dict.get('Windows account', 'N/A')}</div>
                <div class="asset-item"><strong>User Email:</strong> {asset_dict.get('User Email', 'N/A')}</div>
            </div></div>
        </div>
        <style> /* Redefining for Gradio HTML component, as main CSS might not apply */
        .asset-dashboard {{ font-family: Arial, sans-serif; max-width: 100%; margin: 0 auto; padding: 20px; background-color: #f9f9f9; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }}
        .asset-header {{ border-bottom: 2px solid #ddd; padding-bottom: 10px; margin-bottom: 20px; }}
        .asset-header h2 {{ margin: 0; color: #333; }}
        .asset-section {{ margin-bottom: 20px; padding: 15px; background-color: white; border-radius: 5px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }}
        .asset-section h3 {{ margin-top: 0; color: #444; border-bottom: 1px solid #eee; padding-bottom: 8px; }}
        .asset-grid {{ display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 15px; }}
        .asset-item {{ padding: 10px; background-color: #f5f5f5; border-radius: 4px; }}
        .asset-item strong {{ color: #555; }}
        pre {{ white-space: pre-wrap; word-wrap: break-word; background-color: #f0f0f0; padding: 8px; border-radius: 4px; margin: 5px 0; max-height: 200px; overflow-y: auto; }}
        </style>
        """
        return html
    except Exception as e:
        print(f"Error parsing asset details: {e}")
        return f"""<div class="asset-details-error">
            <h3>Error</h3>
            <p>Error retrieving asset details: {str(e)}</p>
        </div>"""

def get_unique_values(column):
    if column == "OS Version" and 'Normalized OS' in assets_df.columns:
         return assets_df['Normalized OS'].dropna().unique().tolist()
    elif column in assets_df.columns:
        values = assets_df[column].dropna().unique().tolist()
        return values
    return []

css = """
body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f3f3f3; color: #1f1f1f; }
.gradio-container { box-shadow: 0 0 10px rgba(0, 0, 0, 0.1); border-radius: 8px; overflow: hidden; }
.gradio-tabs { background-color: #ffffff; }
.gradio-tabs button { color: #1f1f1f; border-bottom: 2px solid transparent; }
.gradio-tabs button.selected { color: #0078d4; border-bottom-color: #0078d4; }
.gradio-textbox, .gradio-dropdown, .gradio-number { border-radius: 4px; border: 1px solid #cccccc; padding: 8px; }
.gradio-button { background-color: #e6e6e6; color: #1f1f1f; border: none; border-radius: 4px; padding: 8px 16px; cursor: pointer; transition: background-color 0.2s ease; }
.gradio-button:hover { background-color: #d4d4d4; }
.gradio-button.primary { background-color: #0078d4; color: #ffffff; }
.gradio-button.primary:hover { background-color: #005a9e; }
.gradio-html table { width: 100%; border-collapse: collapse; margin-top: 10px; }
.gradio-html th, .gradio-html td { border: 1px solid #dddddd; text-align: left; padding: 8px; }
.gradio-html th { background-color: #f2f2f2; }
.gradio-html tr:nth-child(even) { background-color: #f9f9f9; }
.gradio-json { background-color: #ffffff; border: 1px solid #cccccc; border-radius: 4px; padding: 10px; white-space: pre-wrap; word-wrap: break-word; }
"""

def asset_details_api(hostname):
    return get_asset_details(hostname)

def show_asset_details_py_modal(hostname):
    details_html = get_asset_details(hostname) 
    return {
        py_modal_wrapper: gr.update(visible=True),
        py_modal_content_area: gr.update(value=details_html)
    }

def close_asset_details_py_modal():
    return { py_modal_wrapper: gr.update(visible=False) }

def trigger_py_modal_test():
    if not assets_df.empty:
        test_hostname = assets_df.iloc[0]['Hostname']
        return show_asset_details_py_modal(test_hostname) 
    else:
        return {
            py_modal_wrapper: gr.update(visible=True),
            py_modal_content_area: gr.update(value="<p>No assets to display.</p>")
        }

with gr.Blocks(title="CED Asset Manager & Dashboard", css=css) as demo:
    demo.api_name = "api"
    demo.queue(api_open=True)

    py_modal_trigger_hostname_input = gr.Textbox(label="Python Modal Trigger", visible=False, elem_id="py_modal_trigger_hostname_input")
    current_os_filter_state = gr.Textbox(label="Current OS Filter State", value="", visible=False) # Hidden state for OS filter

    with gr.Column(visible=False, elem_id="py_modal_wrapper") as py_modal_wrapper: 
        py_modal_content_area = gr.HTML(value="<p>Modal Content Will Load Here...</p>")
        py_close_modal_button = gr.Button("Close Modal")

    gr.Markdown("# CED Asset Manager & Dashboard\nThis dashboard displays information from IT assets in the network. Use the filters below to find specific assets.")
    
    with gr.Tab("Asset Dashboard"):
        with gr.Row():
            # Column for Results Display (defined first)
            with gr.Column(scale=2):
                results_table = gr.HTML(initial_table) 
                with gr.Row():
                    page_info = gr.Markdown(initial_total_pages) 
            
            # Column for Filters and Controls
            with gr.Column(scale=1):
                # Define general filters first
                hostname_filter = gr.Textbox(label="Filter by Hostname")
                manufacturer_filter = gr.Textbox(label="Filter by Manufacturer")
                with gr.Row(): 
                    min_ram = gr.Number(label="Min RAM (GB)")
                    max_ram = gr.Number(label="Max RAM (GB)")

                # Then define OS-specific filters
                gr.Markdown("### Filter by OS:")
                with gr.Row(elem_id="os_filter_buttons_container"): 
                    for os_name_val in unique_normalized_oss: 
                        btn = gr.Button(os_name_val)
                        # Define the click function using a lambda that captures os_name_val
                        # The lambda's arguments (h_val, m_val, etc.) will receive values from the 'inputs' list
                        click_fn = lambda h_val, m_val, min_r_val, max_r_val, current_os=os_name_val: (
                            (lambda res: { # Inner lambda to unpack result
                                results_table: gr.update(value=res[0]),
                                page_info: gr.update(value=res[1]),
                                current_os_filter_state: current_os # Update state
                            })(filter_assets( # Call filter_assets once
                                hostname_filter=h_val, os_filter=current_os, manufacturer_filter=m_val,
                                min_ram=min_r_val, max_ram=max_r_val, filter_low_storage=False,
                                page_index=0, page_size=None
                            ))
                        )
                        btn.click(
                            fn=click_fn,
                            inputs=[hostname_filter, manufacturer_filter, min_ram, max_ram],
                            outputs=[results_table, page_info, current_os_filter_state] # Add state to outputs
                        )
                
                clear_os_filter = gr.Button("Clear OS Filter", variant="secondary")
                
                # Other buttons that also need access to filters
                low_storage_filter_button = gr.Button("Show Low Storage Assets", variant="secondary")
                filter_button = gr.Button("Apply Filters", elem_id="apply_filters_button")
                test_py_modal_trigger = gr.Button("Test Python Modal (First Asset)")

    with gr.Tab("Asset Details"):
        with gr.Row():
            with gr.Column(scale=1):
                hostname_dropdown = gr.Dropdown(choices=get_unique_values("Hostname"), label="Select Hostname")
                view_details_button = gr.Button("View Details")
            with gr.Column(scale=2):
                asset_details = gr.HTML(label="Asset Details", value="<div class='asset-details-placeholder'><h3>Select a hostname to view asset details</h3><p>Click on a hostname in the results table or select one from the dropdown.</p></div>") 
    
    def create_os_pie_chart():
        if assets_df.empty or 'Normalized OS' not in assets_df.columns: return None
        os_counts = assets_df['Normalized OS'].value_counts().reset_index()
        os_counts.columns = ['Normalized OS', 'Count']
        fig = px.pie(os_counts, values='Count', names='Normalized OS', title='Normalized OS Distribution')
        return fig

    def create_manufacturer_pie_chart():
        if assets_df.empty or 'System Manufacturer' not in assets_df.columns: return None
        manufacturer_counts = assets_df['System Manufacturer'].value_counts().reset_index()
        manufacturer_counts.columns = ['System Manufacturer', 'Count']
        fig = px.pie(manufacturer_counts, values='Count', names='System Manufacturer', title='System Manufacturer Distribution')
        return fig

    with gr.Tab("System Statistics"):
        gr.Markdown("## System Statistics Visualizations\nHere are some visualizations about the assets in the system:")
        with gr.Row():
            os_chart = gr.Plot(label="OS Distribution") 
            manufacturer_chart = gr.Plot(label="System Manufacturer Distribution") 
        generate_charts_button = gr.Button("Generate Charts")

    demo.load(fn=lambda: (create_os_pie_chart(), create_manufacturer_pie_chart()), outputs=[os_chart, manufacturer_chart])
    
    apply_filters_lambda = lambda h, m, min_r, max_r, current_os_val: ( # Add current_os_val
        (lambda res: {
            results_table: gr.update(value=res[0]),
            page_info: gr.update(value=res[1])
        })(filter_assets(
            hostname_filter=h, os_filter=current_os_val, manufacturer_filter=m, # Use current_os_val
            min_ram=min_r, max_ram=max_r, filter_low_storage=False,
            page_index=0, page_size=None
        ))
    )
    filter_button.click(
        fn=apply_filters_lambda,
        inputs=[hostname_filter, manufacturer_filter, min_ram, max_ram, current_os_filter_state], # Add state to inputs
        outputs=[results_table, page_info]
    )

    low_storage_lambda = lambda h, m, min_r, max_r, current_os_val: ( # Add current_os_val
        (lambda res: {
            results_table: gr.update(value=res[0]),
            page_info: gr.update(value=res[1])
        })(filter_assets(
            hostname_filter=h, os_filter=current_os_val, manufacturer_filter=m, # Use current_os_val
            min_ram=min_r, max_ram=max_r, filter_low_storage=True, # filter_low_storage=True here
            page_index=0, page_size=None
        ))
    )
    low_storage_filter_button.click(
        fn=low_storage_lambda,
        inputs=[hostname_filter, manufacturer_filter, min_ram, max_ram, current_os_filter_state], # Add state to inputs
        outputs=[results_table, page_info]
    )

    clear_os_filter_lambda = lambda h_val, m_val, min_r_val, max_r_val: (
        (lambda res: {
            results_table: gr.update(value=res[0]),
            page_info: gr.update(value=res[1]),
            current_os_filter_state: "" # Clear state
        })(filter_assets(
            hostname_filter=h_val, os_filter="", manufacturer_filter=m_val,
            min_ram=min_r_val, max_ram=max_r_val, filter_low_storage=False,
            page_index=0, page_size=None
        ))
    )
    clear_os_filter.click(
        fn=clear_os_filter_lambda,
        inputs=[hostname_filter, manufacturer_filter, min_ram, max_ram],
        outputs=[results_table, page_info, current_os_filter_state] # Add state to outputs
    )
    py_close_modal_button.click(fn=close_asset_details_py_modal, inputs=None, outputs=[py_modal_wrapper])
    test_py_modal_trigger.click(fn=trigger_py_modal_test, inputs=None, outputs=[py_modal_wrapper, py_modal_content_area])
    py_modal_trigger_hostname_input.change(fn=show_asset_details_py_modal, inputs=[py_modal_trigger_hostname_input], outputs=[py_modal_wrapper, py_modal_content_area])
    view_details_button.click(fn=show_asset_details_py_modal, inputs=hostname_dropdown, outputs=[py_modal_wrapper, py_modal_content_area])

if __name__ == "__main__":
    demo.launch(server_name="0.0.0.0", server_port=7867)
