import streamlit as st
import pandas as pd
import plotly.express as px
import plotly.graph_objects as go
from typing import Dict, Any, List
import json

class DashboardComponents:
    """Reusable components for the IT Asset Management Dashboard"""
    
    def render_system_info(self, asset: Dict[str, Any]):
        """Render system information for an asset"""
        st.subheader("System Information")
        
        col1, col2 = st.columns(2)
        
        with col1:
            if asset.get('computer_name'):
                st.write("**Computer Name:**", asset['computer_name'])
            if asset.get('system_info', {}).get('manufacturer'):
                st.write("**Manufacturer:**", asset['system_info']['manufacturer'])
            if asset.get('system_info', {}).get('model'):
                st.write("**Model:**", asset['system_info']['model'])
            if asset.get('system_info', {}).get('bios_version'):
                st.write("**BIOS Version:**", asset['system_info']['bios_version'])
            if asset.get('user_email'):
                st.write("**User Email:**", asset['user_email'])
        
        with col2:
            if asset.get('os_info', {}).get('version'):
                st.write("**Operating System:**", asset['os_info']['version'])
            if asset.get('os_info', {}).get('activation'):
                st.write("**OS Activation:**", asset['os_info']['activation'])
            if asset.get('os_info', {}).get('language'):
                st.write("**Windows Language:**", asset['os_info']['language'])
            if asset.get('anydesk_id'):
                st.write("**AnyDesk ID:**", asset['anydesk_id'])
            st.write("**Last Modified:**", asset.get('last_modified', 'N/A'))
        
        # Stored Network Credentials
        stored_creds = asset.get('stored_credentials', [])
        if stored_creds:
            st.write("**Stored Network Credentials:**")
            for cred in stored_creds:
                st.write(f"• {cred}")
        
        # Shared Folders
        shared_folders = asset.get('shared_folders', [])
        if shared_folders:
            st.write("**Shared Folders:**")
            for folder in shared_folders:
                st.write(f"• {folder}")

    def render_hardware_info(self, asset: Dict[str, Any]):
        """Render hardware information for an asset"""
        st.subheader("Hardware Information")
        
        col1, col2 = st.columns(2)
        
        with col1:
            # Processor information
            processor_info = asset.get('hardware_info', {}).get('processor', {})
            if processor_info.get('name'):
                st.write("**CPU:**", processor_info['name'])
            
            # GPU information
            if asset.get('hardware_info', {}).get('gpu'):
                st.write("**GPU:**", asset['hardware_info']['gpu'])
            
            # Memory information
            memory_info = asset.get('hardware_info', {}).get('memory', {})
            if memory_info.get('total_gb'):
                st.metric("Total RAM", f"{int(memory_info['total_gb'])} GB")
        
        with col2:
            # Network mode (DHCP/Static)
            network_mode = asset.get('network_info', {}).get('mode')
            if network_mode:
                st.write("**Network Mode:**", network_mode)
            elif asset.get('network_info', {}).get('ip_address'):
                # Try to determine from IP if mode not specified
                ip = asset['network_info']['ip_address']
                if ip.startswith('169.254'):
                    st.write("**Network Mode:** DHCP (APIPA)")
                else:
                    st.write("**Network Mode:** Unknown")
        
        # Storage information with C drive highlighting
        storage_info = asset.get('hardware_info', {}).get('storage', [])
        if storage_info:
            st.write("**Storage Devices:**")
            storage_data = []
            for device in storage_info:
                size_gb = device.get('size_gb', 0)
                free_gb = device.get('free_space_gb')
                
                row = {
                    'Device': device.get('name', 'Unknown'),
                    'Size (GB)': f"{size_gb:.1f}" if size_gb else 'N/A'
                }
                
                if free_gb is not None:
                    row['Free Space (GB)'] = f"{free_gb:.1f}"
                    if free_gb < 10:
                        row['Status'] = "🔴 Low Space"
                    elif free_gb < 50:
                        row['Status'] = "🟡 Moderate"
                    else:
                        row['Status'] = "🟢 Good"
                else:
                    row['Free Space (GB)'] = 'N/A'
                    row['Status'] = 'Unknown'
                
                storage_data.append(row)
            
            if storage_data:
                df_storage = pd.DataFrame(storage_data)
                st.dataframe(df_storage, use_container_width=True)

    def render_software_info(self, asset: Dict[str, Any]):
        """Render software information for an asset"""
        st.subheader("Software Information")
        
        # Office and Antivirus information
        col1, col2 = st.columns(2)
        
        with col1:
            office_version = asset.get('software_info', {}).get('office_version')
            if office_version:
                st.write("**Office Version:**", office_version)
            
            antivirus = asset.get('software_info', {}).get('antivirus')
            if antivirus:
                st.write("**Antivirus:**", antivirus)
        
        # Adobe/Autodesk software
        adobe_autodesk = asset.get('software_info', {}).get('adobe_autodesk', [])
        if adobe_autodesk:
            st.write("**Adobe/Autodesk Software:**")
            for software in adobe_autodesk:
                st.write(f"• {software}")
        
        # Installed programs
        software_list = asset.get('software_info', {}).get('installed_programs', [])
        
        if software_list:
            st.write(f"**All Installed Programs ({len(software_list)} total):**")
            
            # Search functionality
            search_term = st.text_input("Search software:", placeholder="Enter software name...")
            
            # Filter software list based on search
            if search_term:
                filtered_software = [
                    software for software in software_list 
                    if search_term.lower() in software.lower()
                ]
            else:
                filtered_software = software_list
            
            # Display software list
            if filtered_software:
                # Create a DataFrame for better display
                software_df = pd.DataFrame({
                    'Software Name': filtered_software
                })
                st.dataframe(software_df, use_container_width=True, height=300)
                
                # Download button for software list
                csv_data = software_df.to_csv(index=False)
                st.download_button(
                    label="Download Software List",
                    data=csv_data,
                    file_name=f"software_list_{asset.get('computer_name', 'unknown')}.csv",
                    mime="text/csv"
                )
            else:
                st.info("No software found matching the search criteria.")
        else:
            st.info("No software information available for this asset.")

    def render_network_info(self, asset: Dict[str, Any]):
        """Render network information for an asset"""
        st.subheader("Network Information")
        
        network_info = asset.get('network_info', {})
        
        if network_info:
            col1, col2 = st.columns(2)
            
            with col1:
                if network_info.get('ip_address'):
                    st.write("**IP Address:**", network_info['ip_address'])
                
                if network_info.get('mac_address'):
                    st.write("**MAC Address:**", network_info['mac_address'])
                
                if network_info.get('ip_address'):
                    st.write("**IP Address:**", network_info['ip_address'])

                if network_info.get('mac_address'):
                    st.write("**MAC Address:**", network_info['mac_address'])

                # Display Nmap Scan Status first, then fallback to general status
                nmap_scan_status = network_info.get('nmap_scan_status')
                general_status = network_info.get('status', 'unknown') # This is the nmap determined status or parser status
                nmap_error = network_info.get('nmap_error')

                if nmap_scan_status == 'scanning':
                    st.info("Nmap Status: Scanning... 🔬")
                elif nmap_scan_status == 'pending':
                    st.warning("Nmap Status: Scan pending... 🕒") # Using warning for pending
                elif nmap_scan_status == 'failed':
                    st.error(f"Nmap Status: Scan failed ❌")
                    if nmap_error:
                        st.caption(f"Error details: {nmap_error}")
                elif nmap_scan_status == 'completed' or nmap_scan_status == 'disabled' or not nmap_scan_status:
                    # If completed, disabled, or status field doesn't exist (e.g. old data), show general status
                    if general_status == 'online':
                        st.success(f"Status: {general_status.title()}")
                    elif general_status in ['offline', 'error', 'unknown']: # Treat 'error' from nmap also as offline indication here
                        st.error(f"Status: {general_status.title()}")
                    else: # Should ideally not happen with current status values
                        st.write(f"Status: {general_status.title()}")
                else: # Other potential nmap_scan_status values if any are added later
                     st.write(f"Nmap Status: {nmap_scan_status.title()}")

                # Display Nmap error separately only if it's not already shown by 'failed' status,
                # and if the scan wasn't disabled (no error expected then).
                if nmap_error and nmap_scan_status not in ['failed', 'disabled']:
                    st.expander("Nmap Scan Details (Error/Output)", expanded=False).text(nmap_error)

                # Optionally, show raw nmap output if available and no major error
                nmap_raw_output = network_info.get('nmap_scan_output')
                if nmap_raw_output and not nmap_error :
                    with st.expander("Show Nmap Output", expanded=False):
                        st.text(nmap_raw_output)

            with col2:
                adapters = network_info.get('adapters', [])
                if adapters:
                    st.write("**Network Adapters:**")
                    for adapter in adapters:
                        st.write(f"• {adapter}")
        else:
            st.info("No network information available for this asset.")

    def render_asset_summary_card(self, asset: Dict[str, Any]) -> None:
        """Render a summary card for an asset"""
        computer_name = asset.get('computer_name', 'Unknown')
        os_version = asset.get('os_info', {}).get('version', 'Unknown OS')
        ip_address = asset.get('network_info', {}).get('ip_address', 'No IP')
        status = asset.get('network_info', {}).get('status', 'unknown')
        
        # Create a container for the card
        with st.container():
            col1, col2, col3 = st.columns([2, 2, 1])
            
            with col1:
                st.write(f"**{computer_name}**")
                st.caption(f"OS: {os_version}")
            
            with col2:
                st.write(f"IP: {ip_address}")
                manufacturer = asset.get('system_info', {}).get('manufacturer', 'Unknown')
                st.caption(f"Manufacturer: {manufacturer}")
            
            with col3:
                if status == 'online':
                    st.success("●")
                elif status == 'offline':
                    st.error("●")
                else:
                    st.warning("●")

    def render_comparison_chart(self, assets: Dict[str, Any], metric: str) -> None:
        """Render a comparison chart for a specific metric across assets"""
        if not assets:
            st.info("No assets available for comparison.")
            return
        
        chart_data = []
        asset_names = []
        
        for name, asset in assets.items():
            asset_names.append(name)
            
            if metric == 'memory':
                memory_gb = asset.get('hardware_info', {}).get('memory', {}).get('total_gb', 0)
                chart_data.append(memory_gb or 0)
            elif metric == 'storage':
                storage_devices = asset.get('hardware_info', {}).get('storage', [])
                total_storage = sum(device.get('size_gb', 0) or 0 for device in storage_devices)
                chart_data.append(total_storage)
            elif metric == 'software_count':
                software_count = len(asset.get('software_list', []))
                chart_data.append(software_count)
        
        if chart_data and any(chart_data):
            fig = px.bar(
                x=asset_names,
                y=chart_data,
                title=f"{metric.replace('_', ' ').title()} Comparison",
                labels={'x': 'Assets', 'y': metric.replace('_', ' ').title()}
            )
            
            fig.update_layout(
                xaxis_tickangle=-45,
                height=400,
                paper_bgcolor='rgba(0,0,0,0)',
                plot_bgcolor='rgba(0,0,0,0)',
                font=dict(family="Inter, sans-serif")
            )
            
            st.plotly_chart(fig, use_container_width=True)
        else:
            st.info(f"No {metric} data available for comparison.")

    def render_asset_health_status(self, assets: Dict[str, Any]) -> None:
        """Render overall health status of assets"""
        if not assets:
            return
        
        st.subheader("Asset Health Overview")
        
        online_count = 0
        offline_count = 0
        unknown_count = 0
        
        for asset in assets.values():
            status = asset.get('network_info', {}).get('status', 'unknown')
            if status == 'online':
                online_count += 1
            elif status == 'offline':
                offline_count += 1
            else:
                unknown_count += 1
        
        col1, col2, col3 = st.columns(3)
        
        with col1:
            st.metric("Online", online_count, delta_color="normal")
        
        with col2:
            st.metric("Offline", offline_count, delta_color="inverse")
        
        with col3:
            st.metric("Unknown", unknown_count, delta_color="off")
        
        # Create a donut chart for status distribution
        if online_count + offline_count + unknown_count > 0:
            fig = go.Figure(data=[go.Pie(
                labels=['Online', 'Offline', 'Unknown'],
                values=[online_count, offline_count, unknown_count],
                hole=0.4,
                marker_colors=['#28a745', '#dc3545', '#ffc107']
            )])
            
            fig.update_layout(
                title="Asset Status Distribution",
                height=300,
                showlegend=True,
                paper_bgcolor='rgba(0,0,0,0)',
                plot_bgcolor='rgba(0,0,0,0)',
                font=dict(family="Inter, sans-serif"),
                margin=dict(t=40, b=0, l=0, r=0)
            )
            
            st.plotly_chart(fig, use_container_width=True)

    def render_raw_data_viewer(self, asset: Dict[str, Any]) -> None:
        """Render raw data viewer for debugging purposes"""
        st.subheader("Raw Data (Debug View)")
        
        with st.expander("Show Raw Asset Data"):
            st.json(asset)
        
        if asset.get('raw_content'):
            with st.expander("Show Raw File Content"):
                st.text(asset['raw_content'])
