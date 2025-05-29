import os
import sys
import subprocess
import re
import glob
import pandas as pd
import gradio as gr
import json
from pathlib import Path

# Add these imports at the top
import plotly.express as px
import plotly.graph_objects as go
import matplotlib.pyplot as plt

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
    
    if not flag_file.exists():
        print("First run detected. Installing dependencies...")
        try:
            # Install required packages
            subprocess.check_call([sys.executable, "-m", "pip", "install", "--upgrade", "pip"])
            for package in required_packages:
                print(f"Installing {package}...")
                subprocess.check_call([sys.executable, "-m", "pip", "install", package])
            
            # Create flag file to indicate dependencies have been installed
            flag_file.touch()
            print("All dependencies installed successfully!")
        except Exception as e:
            print(f"Error installing dependencies: {e}")
            sys.exit(1)
    else:
        print("Dependencies already installed.")

# Call the function to check and install dependencies
check_and_install_dependencies()

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
                "OS Activation": r"OS Activation:\s*(.*?)(?:\n|$)"
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

# Dashboard functions
def filter_assets(hostname_filter, os_filter, manufacturer_filter, min_ram, max_ram, filter_low_storage=False, page_index=0, page_size=None):
    filtered_df = assets_df.copy()
    
    # Print debug information
    print(f"Filtering with OS filter: '{os_filter}'")
    print(f"Available OS values: {filtered_df['Normalized OS'].unique().tolist()}")
    
    if hostname_filter:
        filtered_df = filtered_df[filtered_df['Hostname'].str.contains(hostname_filter, case=False, na=False)]
    
    # Use 'Normalized OS' for filtering if os_filter is provided
    if os_filter:
        # Use contains instead of exact match to be more flexible
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
        # Ensure 'C Drive Free Space (GB)' is numeric before comparison
        # assets_df['C Drive Free Space (GB)'] is already numeric or N/A from parsing
        # We need to apply this to filtered_df
        numeric_disk_space = pd.to_numeric(filtered_df['C Drive Free Space (GB)'], errors='coerce')
        filtered_df = filtered_df[numeric_disk_space < 10]

    # Display only relevant columns in the results table
    display_columns = ['IP', 'Hostname', 'Normalized OS', 'CPU', 'RAM', 'System Manufacturer', 'C Drive Free Space (GB)']
    # Ensure all display_columns exist in the filtered_df before selecting
    display_columns = [col for col in display_columns if col in filtered_df.columns]

    display_columns = ['IP', 'Hostname', 'Normalized OS', 'CPU', 'RAM', 'System Manufacturer', 'C Drive Free Space (GB)']
    display_columns = [col for col in display_columns if col in filtered_df.columns]

    # --- Display Logic ---
    total_items = len(filtered_df)
    
    # If page_size is None, show all items on a single page
    if page_size is None:
        display_df = filtered_df[display_columns].copy()
        total_pages = 1
        page_index = 0
    else:
        # Traditional pagination logic
        total_pages = (total_items + page_size - 1) // page_size # Calculate total pages
        start_index = page_index * page_size
        end_index = min(start_index + page_size, total_items)
        display_df = filtered_df[display_columns].iloc[start_index:end_index].copy()
    
    # Convert numeric columns to string before generating HTML
    numeric_cols_to_convert = ["C Drive Free Space (GB)"]
    for col in numeric_cols_to_convert:
        if col in display_df.columns:
            display_df[col] = display_df[col].astype(str)
    
    # --- End Display Logic ---

    # Create a responsive grid of asset bubbles with popup functionality
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
        cursor: pointer;
        text-align: center;
    }
    
    .asset-bubble:hover {
        transform: translateY(-5px);
        box-shadow: 0 6px 12px rgba(0,0,0,0.15);
    }
    
    .asset-bubble.windows10 {
        background: linear-gradient(135deg, #00a2ed, #0078d7);
    }
    
    .asset-bubble.windows11 {
        background: linear-gradient(135deg, #0078d7, #0063b1);
    }
    
    .asset-bubble.windows8 {
        background: linear-gradient(135deg, #00b2f0, #0072c6);
    }
    
    .asset-bubble.windows7 {
        background: linear-gradient(135deg, #6a737b, #36454f);
    }
    
    .asset-bubble.linux {
        background: linear-gradient(135deg, #f57c00, #d84315);
    }
    
    .asset-bubble.macos {
        background: linear-gradient(135deg, #8e8e93, #636366);
    }
    
    .asset-bubble.unknown {
        background: linear-gradient(135deg, #9e9e9e, #616161);
    }
    
    .asset-hostname {
        font-size: 18px;
        font-weight: bold;
        margin-bottom: 5px;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
    }
    
    .asset-ip {
        font-size: 14px;
        opacity: 0.9;
    }
    
    .asset-os {
        font-size: 12px;
        margin-top: 8px;
        opacity: 0.8;
        background-color: rgba(255, 255, 255, 0.2);
        border-radius: 10px;
        padding: 3px 8px;
        display: inline-block;
    }
    
    .asset-ram {
        font-size: 12px;
        margin-top: 5px;
        opacity: 0.8;
    }

    .asset-disk-space {
        font-size: 12px;
        margin-top: 5px;
        opacity: 0.8;
    }

    .asset-bubble.low-storage-alert {
        border: 3px solid #FF6347; /* Tomato red border */
    }

    .low-storage-warning-text {
        font-weight: bold;
        color: #FF6347; /* Tomato red text */
        font-size: 12px;
        margin-top: 5px;
    }
    
    .pagination-info {
        text-align: center;
        margin: 10px 0;
        font-weight: bold;
    }
    
    /* Modal Popup Styles */
    .asset-modal {
        display: none;
        position: fixed;
        z-index: 1000;
        left: 0;
        top: 0;
        width: 100%;
        height: 100%;
        overflow: auto;
        background-color: rgba(0,0,0,0.5);
    }
    
    .asset-modal-content {
        background-color: #fefefe;
        margin: 5% auto;
        padding: 20px;
        border-radius: 12px;
        width: 80%;
        max-width: 900px;
        box-shadow: 0 5px 15px rgba(0,0,0,0.3);
        position: relative;
        max-height: 85vh;
        overflow-y: auto;
    }
    
    .close-modal {
        color: #aaa;
        float: right;
        font-size: 28px;
        font-weight: bold;
        cursor: pointer;
        position: absolute;
        right: 20px;
        top: 10px;
    }
    
    .close-modal:hover {
        color: black;
    }
    
    @media (max-width: 768px) {
        .asset-bubble {
            min-width: 150px;
        }
        .asset-modal-content {
            width: 95%;
            margin: 10% auto;
        }
    }
    </style>
    
    <!-- Asset Modal Popup -->
    <div id="assetModal" class="asset-modal">
        <div class="asset-modal-content">
            <span class="close-modal" onclick="closeAssetModal()">&times;</span>
            <div id="assetModalContent">
                <!-- Asset details will be loaded here -->
            </div>
        </div>
    </div>
    
    <div class="asset-grid-container">
    """
    
    if not display_df.empty:
        for _, row in display_df.iterrows():
            hostname = row.get('Hostname', 'Unknown')
            ip = row.get('IP', 'N/A')
            os_version = row.get('Normalized OS', 'Unknown OS')
            ram = row.get('RAM', 'N/A')
            free_space_gb_raw = row.get('C Drive Free Space (GB)', 'N/A') # Get raw value first
            
            # Determine the CSS class based on OS
            css_class = "unknown"
            if "Windows 10" in os_version:
                css_class = "windows10"
            elif "Windows 11" in os_version:
                css_class = "windows11"
            elif "Windows 8" in os_version:
                css_class = "windows8"
            elif "Windows 7" in os_version:
                css_class = "windows7"
            elif "Linux" in os_version:
                css_class = "linux"
            elif "macOS" in os_version:
                css_class = "macos"

            low_storage_alert_class = ""
            low_storage_warning_html = ""

            try:
                # Convert free_space_gb_raw to float for comparison
                free_space_gb_val = float(free_space_gb_raw)
                if free_space_gb_val < 10:
                    low_storage_alert_class = "low-storage-alert"
                    low_storage_warning_html = '<div class="low-storage-warning-text">LOW STORAGE!</div>'
            except ValueError:
                # Handle cases where free_space_gb_raw is "N/A" or not a valid number
                pass # Keep default empty strings for class and warning HTML
            
            # Create a bubble for each asset
            html_content += f"""
            <div class="asset-bubble {css_class} {low_storage_alert_class}" onclick="openAssetDetails('{hostname}')">
                <div class="asset-hostname">{hostname}</div>
                <div class="asset-ip">{ip}</div>
                <div class="asset-os">{os_version}</div>
                <div class="asset-ram">{ram}</div>
                <div class="asset-disk-space">Disk: {free_space_gb_raw} GB</div>
                {low_storage_warning_html}
            </div>
            """
    else:
        html_content += """
        <div style="text-align: center; width: 100%; padding: 20px;">
            <p>No assets found matching the current filters.</p>
        </div>
        """
    
    html_content += "</div>"
    
    # Add asset count info
    html_content += f"""
    <div class="pagination-info">
        Showing {total_items} assets
    </div>
    """
    
    # Add JavaScript to handle asset details popup
    js_script = """
    <script>
    // Function to fetch asset details and open modal
    async function openAssetDetails(hostname) {
        const modal = document.getElementById('assetModal');
        const modalContent = document.getElementById('assetModalContent');
        
        // Show loading indicator
        modalContent.innerHTML = '<div style="text-align: center; padding: 30px;"><p>Loading asset details...</p></div>';
        modal.style.display = 'block';
        
        try {
            // Fetch asset details using Gradio API
            const response = await fetch(`/api/get_asset_details?hostname=${encodeURIComponent(hostname)}`);
            if (!response.ok) {
                throw new Error('Failed to fetch asset details');
            }
            
            const data = await response.json();
            
            // Display the asset details in the modal
            modalContent.innerHTML = data.data;
        } catch (error) {
            console.error('Error fetching asset details:', error);
            modalContent.innerHTML = `
                <div style="text-align: center; padding: 30px;">
                    <h3>Error</h3>
                    <p>Failed to load details for ${hostname}</p>
                </div>
            `;
        }
    }
    
    // Function to close the modal
    function closeAssetModal() {
        const modal = document.getElementById('assetModal');
        modal.style.display = 'none';
    }
    
    // Close modal when clicking outside of it
    window.onclick = function(event) {
        const modal = document.getElementById('assetModal');
        if (event.target === modal) {
            modal.style.display = 'none';
        }
    }
    
    // Original function to navigate to asset details tab (kept for compatibility)
    function selectHostname(hostname) {
        // Find the hostname dropdown and set its value
        const dropdowns = document.querySelectorAll('select');
        for (const dropdown of dropdowns) {
            const options = dropdown.options;
            for (let i = 0; i < options.length; i++) {
                if (options[i].text === hostname) {
                    dropdown.value = options[i].value;
                    dropdown.dispatchEvent(new Event('change'));
                    
                    // Find and click the view details button
                    const buttons = document.querySelectorAll('button');
                    for (const button of buttons) {
                        if (button.textContent.includes('View Details')) {
                            button.click();
                            break;
                        }
                    }
                    break;
                }
            }
        }
    }
    </script>
    """
    
    # Return the HTML content with the JavaScript and the formatted asset count info
    if page_size is None:
        return html_content + js_script, f"Showing all {total_items} assets"
    else:
        return html_content + js_script, str(f"Page {page_index + 1} of {total_pages}")

# Initial call to filter_assets needs to return both table and total_pages
# Pass None as page_size to show all assets on a single page
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
        # Convert to dictionary and ensure all values are JSON-serializable
        asset_dict = selected_asset.iloc[0].to_dict()
        # Ensure all values are strings or "N/A"
        for k, v in asset_dict.items():
            if pd.isna(v):
                asset_dict[k] = "N/A"
            elif not isinstance(v, str):
                asset_dict[k] = str(v)
        
        # Create a user-friendly HTML dashboard with popup styling
        html = f"""
        <style>
        .asset-dashboard {{
            background: white;
            border-radius: 12px;
            box-shadow: 0 8px 30px rgba(0,0,0,0.2);
            padding: 25px;
            max-width: 900px;
            margin: 0 auto;
            position: relative;
        }}
        
        .asset-header {{
            border-bottom: 2px solid #f0f0f0;
            padding-bottom: 15px;
            margin-bottom: 20px;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }}
        
        .asset-header h2 {{
            margin: 0;
            color: #2c3e50;
            font-size: 24px;
        }}
        
        .asset-header-right {{
            text-align: right;
        }}
        
        .asset-section {{
            margin-bottom: 25px;
        }}
        
        .asset-section h3 {{
            color: #3498db;
            border-left: 4px solid #3498db;
            padding-left: 10px;
            margin-bottom: 15px;
        }}
        
        .asset-grid {{
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
            gap: 15px;
        }}
        
        .asset-item {{
            background: #f8f9fa;
            padding: 12px;
            border-radius: 8px;
        }}
        
        .asset-item strong {{
            color: #34495e;
        }}
        
        .user-accounts-section {{
            margin-top: 20px;
        }}
        
        .software-list {{
            max-height: 300px;
            overflow-y: auto;
            border: 1px solid #eee;
            padding: 10px;
            border-radius: 8px;
        }}
        </style>
        
        <div class="asset-dashboard">
            <div class="asset-header">
                <div>
                    <h2>{asset_dict.get('Hostname', 'N/A')}</h2>
                    <p>IP: {asset_dict.get('IP', 'N/A')}</p>
                </div>
                <div class="asset-header-right">
                    <p><strong>Last Seen:</strong> {asset_dict.get('Last Seen', 'N/A')}</p>
                    <p><strong>Status:</strong> <span style="color: green;">Active</span></p>
                </div>
            </div>
            
            <div class="asset-section">
                <h3>System Information</h3>
                <div class="asset-grid">
                    <div class="asset-item">
                        <strong>OS:</strong> {asset_dict.get('Normalized OS', 'N/A')}
                    </div>
                    <div class="asset-item">
                        <strong>CPU:</strong> {asset_dict.get('CPU', 'N/A')}
                    </div>
                    <div class="asset-item">
                        <strong>RAM:</strong> {asset_dict.get('RAM', 'N/A')}
                    </div>
                    <div class="asset-item">
                        <strong>GPU:</strong> {asset_dict.get('GPU', 'N/A')}
                    </div>
                    <div class="asset-item">
                        <strong>Manufacturer:</strong> {asset_dict.get('System Manufacturer', 'N/A')}
                    </div>
                    <div class="asset-item">
                        <strong>Model:</strong> {asset_dict.get('System Model', 'N/A')}
                    </div>
                </div>
            </div>
            
            <div class="asset-section">
                <h3>Storage</h3>
                <div class="asset-grid">
                    <div class="asset-item">
                        <strong>C Drive Free Space:</strong> {asset_dict.get('C Drive Free Space (GB)', 'N/A')} GB
                    </div>
                    <div class="asset-item">
                        <strong>Disk Info:</strong> <pre>{asset_dict.get('Disk Info', 'N/A')}</pre>
                    </div>
                </div>
            </div>
            
            <div class="asset-section">
                <h3>Software</h3>
                <div class="asset-grid">
                    <div class="asset-item">
                        <strong>Windows Language:</strong> {asset_dict.get('Windows Language', 'N/A')}
                    </div>
                    <div class="asset-item">
                        <strong>Antivirus:</strong> {asset_dict.get('Antivirus', 'N/A')}
                    </div>
                    <div class="asset-item">
                        <strong>Office Version:</strong> {asset_dict.get('Office Version', 'N/A')}
                    </div>
                    <div class="asset-item">
                        <strong>OS Activation:</strong> {asset_dict.get('OS Activation', 'N/A')}
                    </div>
                </div>
            </div>
            
            <div class="asset-section">
                <h3>User Accounts</h3>
                <div class="asset-grid">
                    <div class="asset-item">
                        <strong>Current User:</strong> {asset_dict.get('Current User', 'N/A')}
                    </div>
                    <div class="asset-item">
                        <strong>Domain:</strong> {asset_dict.get('Domain', 'N/A')}
                    </div>
                </div>
                
                <div class="user-accounts-section">
                    <h4>User Email Accounts</h4>
                    <div class="asset-item">
                        <ul>
                            <li><strong>Primary Email:</strong> user@company.com</li>
                            <li><strong>Secondary Email:</strong> user.backup@company.com</li>
                        </ul>
                    </div>
                </div>
            </div>
            
            <div class="asset-section">
                <h3>Installed Software</h3>
                <div class="software-list">
                    <ul>
                        <li>Microsoft Office 365</li>
                        <li>Google Chrome</li>
                        <li>Mozilla Firefox</li>
                        <li>Adobe Acrobat Reader</li>
                        <li>7-Zip</li>
                        <li>VLC Media Player</li>
                        <li>Microsoft Teams</li>
                        <li>Zoom</li>
                        <li>Slack</li>
                        <li>Notepad++</li>
                    </ul>
                </div>
            </div>
            
            <div class="asset-section">
                <h3>Network</h3>
                <div class="asset-grid">
                    <div class="asset-item">
                        <strong>PC Domain:</strong> {asset_dict.get('PC Domain', 'N/A')}
                    </div>
                    <div class="asset-item">
                        <strong>AnyDesk ID:</strong> {asset_dict.get('AnyDesk ID', 'N/A')}
                    </div>
                    <div class="asset-item">
                        <strong>Windows Account:</strong> {asset_dict.get('Windows account', 'N/A')}
                    </div>
                    <div class="asset-item">
                        <strong>User Email:</strong> {asset_dict.get('User Email', 'N/A')}
                    </div>
                </div>
            </div>
        </div>
        
        <style>
        .asset-dashboard {
            font-family: Arial, sans-serif;
            max-width: 100%;
            margin: 0 auto;
            padding: 20px;
            background-color: #f9f9f9;
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }
        
        .asset-header {
            border-bottom: 2px solid #ddd;
            padding-bottom: 10px;
            margin-bottom: 20px;
        }
        
        .asset-header h2 {
            margin: 0;
            color: #333;
        }
        
        .asset-section {
            margin-bottom: 20px;
            padding: 15px;
            background-color: white;
            border-radius: 5px;
            box-shadow: 0 1px 3px rgba(0,0,0,0.05);
        }
        
        .asset-section h3 {
            margin-top: 0;
            color: #444;
            border-bottom: 1px solid #eee;
            padding-bottom: 8px;
        }
        
        .asset-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
            gap: 15px;
        }
        
        .asset-item {
            padding: 10px;
            background-color: #f5f5f5;
            border-radius: 4px;
        }
        
        .asset-item strong {
            color: #555;
        }
        
        pre {
            white-space: pre-wrap;
            word-wrap: break-word;
            background-color: #f0f0f0;
            padding: 8px;
            border-radius: 4px;
            margin: 5px 0;
            max-height: 200px;
            overflow-y: auto;
        }
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

def create_os_filter_buttons():
    """Create visually appealing OS filter buttons with counts"""
    if assets_df.empty or 'Normalized OS' not in assets_df.columns:
        return "<p>No OS data available</p>"
    
    # Get OS counts
    os_counts = assets_df['Normalized OS'].value_counts().reset_index()
    os_counts.columns = ['Normalized OS', 'Count']
    
    # Create HTML for the buttons
    html = """
    <style>
    .os-filter-container {
        display: flex;
        flex-wrap: wrap;
        gap: 10px;
        margin-bottom: 15px;
    }
    
    .os-filter-button {
        display: flex;
        flex-direction: column;
        align-items: center;
        padding: 12px 15px;
        border-radius: 8px;
        cursor: pointer;
        transition: all 0.3s ease;
        color: white;
        min-width: 100px;
        text-align: center;
        box-shadow: 0 2px 4px rgba(0,0,0,0.1);
    }
    
    .os-filter-button:hover {
        transform: translateY(-3px);
        box-shadow: 0 4px 8px rgba(0,0,0,0.15);
    }
    
    .os-filter-button.windows10 {
        background: linear-gradient(135deg, #00a2ed, #0078d7);
    }
    
    .os-filter-button.windows11 {
        background: linear-gradient(135deg, #0078d7, #0063b1);
    }
    
    .os-filter-button.windows8 {
        background: linear-gradient(135deg, #00b2f0, #0072c6);
    }
    
    .os-filter-button.windows7 {
        background: linear-gradient(135deg, #6a737b, #36454f);
    }
    
    .os-filter-button.linux {
        background: linear-gradient(135deg, #f57c00, #d84315);
    }
    
    .os-filter-button.macos {
        background: linear-gradient(135deg, #8e8e93, #636366);
    }
    
    .os-filter-button.unknown {
        background: linear-gradient(135deg, #9e9e9e, #616161);
    }
    
    .os-name {
        font-weight: bold;
        margin-bottom: 5px;
    }
    
    .os-count {
        background-color: rgba(255, 255, 255, 0.2);
        border-radius: 10px;
        padding: 2px 8px;
        font-size: 12px;
    }
    </style>
    
    <div class="os-filter-container">
    """
    
    # Add a button for each OS
    for _, row in os_counts.iterrows():
        os_name = row['Normalized OS']
        count = row['Count']
        
        # Determine the CSS class based on OS
        css_class = "unknown"
        if "Windows 10" in os_name:
            css_class = "windows10"
        elif "Windows 11" in os_name:
            css_class = "windows11"
        elif "Windows 8" in os_name:
            css_class = "windows8"
        elif "Windows 7" in os_name:
            css_class = "windows7"
        elif "Linux" in os_name:
            css_class = "linux"
        elif "macOS" in os_name:
            css_class = "macos"
        
        html += f"""
        <div class="os-filter-button {css_class}" onclick="setOsFilter('{os_name}')">
            <div class="os-name">{os_name}</div>
            <div class="os-count">{count}</div>
        </div>
        """
    
    html += """
    </div>
    
    <script>
    function setOsFilter(osName) {
        // Find the hidden OS filter textbox and set its value
        const osFilterInputs = document.querySelectorAll('input[aria-label="Selected OS Filter"]');
        if (osFilterInputs.length > 0) {
            osFilterInputs[0].value = osName;
            osFilterInputs[0].dispatchEvent(new Event('input'));
            
            // Find and click the Apply Filters button
            const buttons = document.querySelectorAll('button');
            for (const button of buttons) {
                if (button.textContent.includes('Apply Filters')) {
                    button.click();
                    break;
                }
            }
        }
    }
    </script>
    """
    
    return html

# Create Gradio interface
# Using gr.Blocks and gr.Row/gr.Column with scale for responsive layout
# Adding CSS for Windows 11 inspired styling
css = """
body {
    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
    background-color: #f3f3f3; /* Light grey background */
    color: #1f1f1f; /* Dark grey text */
}
.gradio-container {
    box-shadow: 0 0 10px rgba(0, 0, 0, 0.1);
    border-radius: 8px;
    overflow: hidden;
}
.gradio-tabs {
    background-color: #ffffff; /* White background for tabs */
}
.gradio-tabs button {
    color: #1f1f1f; /* Dark grey tab text */
    border-bottom: 2px solid transparent;
}
.gradio-tabs button.selected {
    color: #0078d4; /* Windows blue for selected tab */
    border-bottom-color: #0078d4;
}
.gradio-textbox, .gradio-dropdown, .gradio-number {
    border-radius: 4px;
    border: 1px solid #cccccc; /* Light grey border */
    padding: 8px;
}
.gradio-button {
    background-color: #e6e6e6; /* Light grey button */
    color: #1f1f1f; /* Dark grey button text */
    border: none;
    border-radius: 4px;
    padding: 8px 16px;
    cursor: pointer;
    transition: background-color 0.2s ease;
}
.gradio-button:hover {
    background-color: #d4d4d4; /* Slightly darker grey on hover */
}
.gradio-button.primary {
    background-color: #0078d4; /* Windows blue primary button */
    color: #ffffff; /* White text */
}
.gradio-button.primary:hover {
    background-color: #005a9e; /* Darker blue on hover */
}
.gradio-html table {
    width: 100%;
    border-collapse: collapse;
    margin-top: 10px;
}
.gradio-html th, .gradio-html td {
    border: 1px solid #dddddd; /* Light grey table borders */
    text-align: left;
    padding: 8px;
}
.gradio-html th {
    background-color: #f2f2f2; /* Lighter grey header */
}
.gradio-html tr:nth-child(even) {
    background-color: #f9f9f9; /* Very light grey for even rows */
}
.gradio-json {
    background-color: #ffffff; /* White background for JSON */
    border: 1px solid #cccccc;
    border-radius: 4px;
    padding: 10px;
    white-space: pre-wrap; /* Wrap long lines */
    word-wrap: break-word;
}
"""
# Create API endpoint for asset details
def asset_details_api(hostname):
    return get_asset_details(hostname)

with gr.Blocks(title="CED Asset Manager & Dashboard", css=css) as demo:
    # Register the API endpoint
    demo.api_name = "api"
    demo.queue(api_open=True)
    gr.Markdown(
    """
    # CED Asset Manager & Dashboard
    
    This dashboard displays information from IT assets in the network. Use the filters below to find specific assets.
    """
    )
    
    with gr.Tab("Asset Dashboard"):
        # This Row contains two Columns that will adjust width based on scale and stack on small screens
        with gr.Row():
            # Column for filters - takes 1 unit of space
            with gr.Column(scale=1):
                hostname_filter = gr.Textbox(label="Filter by Hostname")
                # Keep os_filter textbox, hide it
                os_filter = gr.Textbox(label="Selected OS Filter", visible=False)

                gr.Markdown("### Filter by OS:")
                
                # Create a custom HTML component for OS filter buttons
                os_filter_html = gr.HTML(value=create_os_filter_buttons())
                
                # Add a "Clear OS Filter" button
                clear_os_filter = gr.Button("Clear OS Filter", variant="secondary")

                manufacturer_filter = gr.Textbox(label="Filter by Manufacturer")
                
                with gr.Row():
                    min_ram = gr.Number(label="Min RAM (GB)")
                    max_ram = gr.Number(label="Max RAM (GB)")
                
                low_storage_filter_button = gr.Button("Show Low Storage Assets", variant="secondary")
                filter_button = gr.Button("Apply Filters")
            
            # Column for results table - takes 2 units of space
            # Column for results table - takes 2 units of space
            with gr.Column(scale=2):
                results_table = gr.HTML(initial_table) # Use initial table content

                # --- Asset Count Info ---
                with gr.Row():
                    page_info = gr.Markdown(initial_total_pages) # Info about total assets
                # --- End Asset Count Info ---

    with gr.Tab("Asset Details"):
        # This Row contains two Columns that will adjust width based on scale and stack on small screens
        with gr.Row():
            # Column for details controls - takes 1 unit of space
            with gr.Column(scale=1):
                hostname_dropdown = gr.Dropdown(
                    choices=get_unique_values("Hostname"),
                    label="Select Hostname"
                )
                view_details_button = gr.Button("View Details")
            
            # Column for asset details JSON - takes 2 units of space
            # Column for asset details JSON - takes 2 units of space
            with gr.Column(scale=2):
                asset_details = gr.HTML(label="Asset Details", value="""
                <div class="asset-details-placeholder">
                    <h3>Select a hostname to view asset details</h3>
                    <p>Click on a hostname in the results table or select one from the dropdown.</p>
                </div>
                """) # Set initial value to empty JSON
    
    # Function to generate OS distribution pie chart

    def create_os_pie_chart():
        if assets_df.empty or 'Normalized OS' not in assets_df.columns:
            return None
        os_counts = assets_df['Normalized OS'].value_counts().reset_index()
        os_counts.columns = ['Normalized OS', 'Count']
        fig = px.pie(os_counts, values='Count', names='Normalized OS', title='Normalized OS Distribution')
        return fig

    # Function to generate System Manufacturer pie chart
    def create_manufacturer_pie_chart():
        if assets_df.empty or 'System Manufacturer' not in assets_df.columns:
            return None
        manufacturer_counts = assets_df['System Manufacturer'].value_counts().reset_index()
        manufacturer_counts.columns = ['System Manufacturer', 'Count']
        fig = px.pie(manufacturer_counts, values='Count', names='System Manufacturer', title='System Manufacturer Distribution')
        return fig

    with gr.Tab("System Statistics"):
        gr.Markdown(
        """
        ## System Statistics Visualizations
        
        Here are some visualizations about the assets in the system:
        """
        )
        # This Row contains two Plots that will share space and stack on small screens
        with gr.Row():
            os_chart = gr.Plot(label="OS Distribution") # Make chart interactive
            manufacturer_chart = gr.Plot(label="System Manufacturer Distribution") # Make chart interactive

        generate_charts_button = gr.Button("Generate Charts")

    # Set up event handlers
    demo.load(
        fn=lambda: (create_os_pie_chart(), create_manufacturer_pie_chart()),
        outputs=[os_chart, manufacturer_chart]
    )

    # Define the handler for OS chart selection
    def handle_os_chart_select(select_data, hostname_filter_value, manufacturer_filter_value, min_ram_value, max_ram_value, page_size_value):
        if select_data and select_data.value:
            # For a pie chart, select_data.value is the name of the selected slice
            selected_os = select_data.value
            print(f"Selected OS from chart: {selected_os}") # Debug print
            # Call the main filter function with the selected OS and reset to page 0
            return filter_assets(hostname_filter_value, selected_os, manufacturer_filter_value, min_ram_value, max_ram_value, page_index=0, page_size=page_size_value)
        # If nothing is selected (e.g., clicking outside a slice), clear the OS filter and reset to page 0
        return filter_assets(hostname_filter_value, "", manufacturer_filter_value, min_ram_value, max_ram_value, page_index=0, page_size=page_size_value)

    # Link the OS chart select event to the handler
    # Update inputs to include pagination controls
    # os_chart.select(
    #     fn=handle_os_chart_select,
    #     inputs=[hostname_filter, manufacturer_filter, min_ram, max_ram, page_size_input], # Pass other filter inputs + page_size
    #     outputs=[results_table, page_info] # Update both table and page info
    # )

    # Define the handler for Manufacturer chart selection
    # Update inputs to include pagination controls
    def handle_manufacturer_chart_select(select_data, hostname_filter_value, os_filter_value, min_ram_value, max_ram_value, page_size_value):
        if select_data and select_data.value:
            selected_manufacturer = select_data.value
            print(f"Selected Manufacturer from chart: {selected_manufacturer}")
            # Call filter_assets with selected manufacturer and reset to page 0
            return filter_assets(hostname_filter_value, os_filter_value, selected_manufacturer, min_ram_value, max_ram_value, page_index=0, page_size=page_size_value)
        # If nothing is selected, clear the Manufacturer filter and reset to page 0
        return filter_assets(hostname_filter_value, os_filter_value, "", min_ram_value, max_ram_value, page_index=0, page_size=page_size_value)

    # Link the Manufacturer chart select event to the handler
    # Update inputs to include pagination controls
    # manufacturer_chart.select(
    #     fn=handle_manufacturer_chart_select,
    #     inputs=[hostname_filter, os_filter, min_ram, max_ram, page_size_input], # Pass other filter inputs + page_size
    #     outputs=[results_table, page_info] # Update both table and page info
    # )

    # The generate_charts_button click handler remains the same
    generate_charts_button.click(
        fn=lambda: (create_os_pie_chart(), create_manufacturer_pie_chart()),
        outputs=[os_chart, manufacturer_chart]
    )

    # Update the filter_button handler to show all results on a single page
    filter_button.click(
        fn=lambda h, o, m, min_r, max_r: filter_assets(h, o, m, min_r, max_r, filter_low_storage=False, page_index=0, page_size=None), # Show all results, ensure low_storage is False
        inputs=[hostname_filter, os_filter, manufacturer_filter, min_ram, max_ram],
        outputs=[results_table, page_info] # Update both table and page info
    )

    # Handler for the new "Show Low Storage Assets" button
    low_storage_filter_button.click(
        fn=lambda h, o, m, min_r, max_r: filter_assets(h, o, m, min_r, max_r, filter_low_storage=True, page_index=0, page_size=None),
        inputs=[hostname_filter, os_filter, manufacturer_filter, min_ram, max_ram],
        outputs=[results_table, page_info]
    )

    # The OS filter buttons now use JavaScript to set the os_filter value and click the Apply Filters button
    
    # Add handler for the Clear OS Filter button
    clear_os_filter.click(
        fn=lambda: "", # Function to clear the OS filter
        inputs=[],
        outputs=os_filter # Clear the os_filter textbox value
    ).then( # Chain the next action
        fn=lambda h, o, m, min_r, max_r: filter_assets(h, o, m, min_r, max_r, filter_low_storage=False, page_index=0, page_size=None), # Call filter_assets with all results
        inputs=[hostname_filter, os_filter, manufacturer_filter, min_ram, max_ram], # Pass all filter inputs
        outputs=[results_table, page_info] # Update both table and page info
    )

    # --- Pagination Button Handlers ---
    # Function to go to the previous page
    def go_to_previous_page(current_page_info, hostname_filter_value, os_filter_value, manufacturer_filter_value, min_ram_value, max_ram_value, page_size_value):
        # Parse current page and total pages from page_info markdown
        match = re.search(r"Page (\d+) of (\d+)", current_page_info)
        if match:
            current_page = int(match.group(1))
            total_pages = int(match.group(2))
            if current_page > 1:
                new_page_index = current_page - 2 # page_index is 0-based
                table_html, new_total_pages = filter_assets(hostname_filter_value, os_filter_value, manufacturer_filter_value, min_ram_value, max_ram_value, page_index=new_page_index, page_size=page_size_value)
                return table_html, f"Page {new_page_index + 1} of {str(new_total_pages)}" # Explicitly convert to string
        # If cannot go back, return current state
        return gr.update(), gr.update()

    # Function to go to the next page
    def go_to_next_page(current_page_info, hostname_filter_value, os_filter_value, manufacturer_filter_value, min_ram_value, max_ram_value, page_size_value):
        # Parse current page and total pages from page_info markdown
        match = re.search(r"Page (\d+) of (\d+)", current_page_info)
        if match:
            current_page = int(match.group(1))
            total_pages = int(match.group(2))
            if current_page < total_pages:
                new_page_index = current_page # page_index is 0-based
                table_html, new_total_pages = filter_assets(hostname_filter_value, os_filter_value, manufacturer_filter_value, min_ram_value, max_ram_value, page_index=new_page_index, page_size=page_size_value)
                return table_html, f"Page {new_page_index + 1} of {str(new_total_pages)}" # Explicitly convert to string
        # If cannot go forward, return current state
        return gr.update(), gr.update()

    # Pagination has been removed - all assets are shown on a single page

    # Page size input has been removed - all assets are shown on a single page
    # --- End Pagination Button Handlers ---


    view_details_button.click(
        fn=get_asset_details,
        inputs=hostname_dropdown,
        outputs=asset_details
    )

# Launch the app
if __name__ == "__main__":
    demo.launch(server_name="0.0.0.0", server_port=7867)

