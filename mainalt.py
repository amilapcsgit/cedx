# main.py
"""
IT-Asset-Dashboard (refactored & hardened)

Run:
    streamlit run main.py
"""

from __future__ import annotations

# ──────────────────────────────── Std-Lib
import asyncio
import concurrent.futures
import html
import ipaddress
import json
import logging
import os
import re
import subprocess
from datetime import datetime
from enum import Enum
from pathlib import Path
from typing import Dict, List, Optional, Tuple

# ──────────────────────────────── Third-Party
import pandas as pd
import plotly.express as px
import streamlit as st

# ──────────────────────────────── Local
from asset_parser import AssetParser          # External helper (provide your own)
# from dashboard_components import DashboardComponents   # optional

# ╭──────────────────────────────────────────────────────────────╮
# │ 1.  CONFIGURATION                                            │
# ╰──────────────────────────────────────────────────────────────╯
class Config:
    NMAP_TIMEOUT            = 120                 # seconds
    MAX_CONCURRENT_SCANS    = 6                  # async workers
    LOW_STORAGE_THRESHOLD_GB = 10
    ASSETS_FOLDER           = Path("assets")
    CACHE_TTL_SECONDS       = 300                # 5 min
    THEME_TOGGLE_EMOJI      = "🌓"
    LOG_LEVEL               = os.getenv("LOG_LEVEL", "INFO").upper()


# ╭──────────────────────────────────────────────────────────────╮
# │ 2.  LOGGING SET-UP                                           │
# ╰──────────────────────────────────────────────────────────────╯
logging.basicConfig(
    level=getattr(logging, Config.LOG_LEVEL, logging.INFO),
    format="%(asctime)s | %(levelname)8s | %(name)s | %(message)s",
)
log = logging.getLogger("main")


# ╭──────────────────────────────────────────────────────────────╮
# │ 3.  ENUM & HELPER UTILITIES                                  │
# ╰──────────────────────────────────────────────────────────────╯
class AssetStatus(str, Enum):
    ONLINE   = "online"
    OFFLINE  = "offline"
    SCANNING = "scanning"
    FAILED   = "failed"
    UNKNOWN  = "unknown"

    @classmethod
    def from_nmap_stdout(cls, stdout: str) -> "AssetStatus":
        if "Host is up" in stdout:
            return cls.ONLINE
        if "Host seems down" in stdout:
            return cls.OFFLINE
        # quick heuristic: open ports line
        if re.search(r"\d+/open/", stdout):
            return cls.ONLINE
        return cls.UNKNOWN


def validate_ip(ip: str | None) -> bool:
    if not ip:
        return False
    try:
        ipaddress.ip_address(ip)
        return True
    except ValueError:
        return False


def escape(s: str) -> str:
    """Escape HTML to prevent XSS."""
    return html.escape(str(s), quote=True)


# ╭──────────────────────────────────────────────────────────────╮
# │ 4.  STREAMLIT PAGE CONFIG & THEME                            │
# ╰──────────────────────────────────────────────────────────────╯
st.set_page_config(
    page_title="IT Asset Management Dashboard",
    page_icon="🖥️",
    layout="wide",
    initial_sidebar_state="expanded",
)

# --- Light/Dark theme CSS (same idea as before, trimmed) ---
def apply_windows11_theme() -> None:
    mode = st.session_state.get("theme_mode", "light")
    bg = "#1e1e1e" if mode == "dark" else "#F4F6F8"
    text = "#fff" if mode == "dark" else "#212529"
    accent = "#0078d4" if mode == "dark" else "#3C82F6"

    st.markdown(
        f"""
        <style>
        .stApp  {{ background-color:{bg}; color:{text}; }}
        a       {{ color:{accent}; }}
        /* Add more CSS here … */
        </style>
        """,
        unsafe_allow_html=True,
    )


# ╭──────────────────────────────────────────────────────────────╮
# │ 5.  MAIN DASHBOARD CLASS                                     │
# ╰──────────────────────────────────────────────────────────────╯
class ITAssetDashboard:
    # ─────────────────────── STATE INIT ────────────────────────
    def __init__(self) -> None:
        self.parser = AssetParser()

        # initialise persistent session-state keys
        defaults = {
            "assets_data":        {},
            "last_refresh":       None,
            "theme_mode":         "light",
            "selected_filters":   {},    # we keep all filter selections in a sub-dict
            "nmap_path":          "nmap",
        }
        for k, v in defaults.items():
            if k not in st.session_state:
                st.session_state[k] = v

    # ╭──────────────────────────────────────────────────────────╮
    # │ 5.1  DATA LOADING + NMAP (ASYNC)                         │
    # ╰──────────────────────────────────────────────────────────╯
    @st.cache_data(ttl=Config.CACHE_TTL_SECONDS, show_spinner=False)
    def _load_raw_assets(self, assets_folder: Path) -> Dict[str, dict]:
        """Parse all *.txt asset files into dicts (no network)."""
        log.info("Scanning %s for asset files", assets_folder)
        assets: Dict[str, dict] = {}

        if not assets_folder.exists():
            log.warning("Assets folder does not exist.")
            return assets

        for f in assets_folder.glob("*.txt"):
            try:
                asset = self.parser.parse_asset_file(f)
                name = asset.get("computer_name", f.stem)
                assets[name] = asset
            except Exception as exc:               # noqa: BLE001
                log.exception("Error parsing %s: %s", f, exc)
        return assets

    async def _nmap_scan_one(
        self, ip: str, scan_type: str = "quick"
    ) -> Tuple[AssetStatus, str, Optional[str]]:
        """
        Run nmap and return (status, stdout, error_msg)
        """
        if not validate_ip(ip):
            return AssetStatus.UNKNOWN, "", "Invalid IP"

        nmap_bin = st.session_state["nmap_path"]
        if scan_type == "quick":
            cmd = [nmap_bin, "-sn", "-T4", ip]
        else:                                     # full scan
            cmd = [nmap_bin, "-T4", "-A", "-Pn", ip]

        log.debug("Running nmap: %s", " ".join(cmd))

        # async wrapper
        loop = asyncio.get_running_loop()
        try:
            proc = await loop.run_in_executor(
                None,                # default executor
                lambda: subprocess.run(
                    cmd,
                    text=True,
                    capture_output=True,
                    timeout=Config.NMAP_TIMEOUT,
                ),
            )
            if proc.returncode == 0:
                status = AssetStatus.from_nmap_stdout(proc.stdout)
                return status, proc.stdout, None
            return (
                AssetStatus.FAILED,
                proc.stdout,
                proc.stderr or f"Return code {proc.returncode}",
            )
        except FileNotFoundError:
            return AssetStatus.FAILED, "", f"{nmap_bin} not found"
        except subprocess.TimeoutExpired:
            return AssetStatus.FAILED, "", "timeout"
        except Exception as exc:                 # noqa: BLE001
            return AssetStatus.FAILED, "", str(exc)

    async def _async_nmap_batch(self, assets: Dict[str, dict]) -> None:
        """
        Concurrently enrich each asset with nmap status (quick scan).
        Updates the dict in-place.
        """
        ips = {
            name: a.get("network_info", {}).get("ip_address")
            for name, a in assets.items()
            if validate_ip(a.get("network_info", {}).get("ip_address"))
        }
        if not ips:
            return

        sem = asyncio.Semaphore(Config.MAX_CONCURRENT_SCANS)

        async def sem_task(name: str, ip: str):
            async with sem:
                status, stdout, err = await self._nmap_scan_one(ip, "quick")
                assets[name].setdefault("network_info", {})
                assets[name]["network_info"]["status"] = status.value
                assets[name]["network_info"]["nmap_quick_stdout"] = stdout
                if err:
                    assets[name]["network_info"]["nmap_error"] = err

        await asyncio.gather(*(sem_task(n, ip) for n, ip in ips.items()))

    # ────────────────────────────────────────────────────────────
    def refresh_data(self) -> None:
        """Public trigger used by 'Refresh' button."""
        with st.spinner("Loading files …"):
            assets = self._load_raw_assets(Config.ASSETS_FOLDER)

        with st.spinner("Running quick Nmap scans …"):
            # run asyncio event-loop in blocking way (safe inside Streamlit)
            asyncio.run(self._async_nmap_batch(assets))

        st.session_state["assets_data"] = assets
        st.session_state["last_refresh"] = datetime.now()

    # ╭──────────────────────────────────────────────────────────╮
    # │ 5.2  SIDEBAR FILTERING                                   │
    # ╰──────────────────────────────────────────────────────────╯
    def sidebar(self) -> dict:
        st.sidebar.header("Filters & options")

        assets = st.session_state["assets_data"]

        # Derive filter value lists once
        os_set, manufacturer_set, ram_vals, c_free_vals = set(), set(), [], []
        for a in assets.values():
            os_set.add(self._norm_os(a.get("os_info", {}).get("version")))
            manufacturer_set.add(a.get("system_info", {}).get("manufacturer", "Unknown"))

            ram = a.get("hardware_info", {}).get("memory", {}).get("total_gb", 0)
            if ram:
                ram_vals.append(int(ram))

            free_c = self._c_drive_free(a)
            if free_c is not None:
                c_free_vals.append(free_c)

        sel = st.session_state["selected_filters"]  # shorthand

        # multi-selects
        sel.setdefault("os", list(os_set))
        sel["os"] = st.sidebar.multiselect("OS", sorted(os_set), default=sel["os"])

        sel.setdefault("manufacturer", list(manufacturer_set))
        sel["manufacturer"] = st.sidebar.multiselect(
            "Manufacturer", sorted(manufacturer_set), default=sel["manufacturer"]
        )

        # sliders
        min_ram, max_ram = (min(ram_vals or [0]), max(ram_vals or [128]))
        sel.setdefault("ram", (min_ram, max_ram))
        sel["ram"] = st.sidebar.slider("RAM (GB)", min_ram, max_ram, sel["ram"])

        min_sto, max_sto = (0.0, max(c_free_vals or [500.0]))
        sel.setdefault("c_free", (min_sto, max_sto))
        sel["c_free"] = st.sidebar.slider(
            "C: free space (GB)", float(min_sto), float(max_sto), sel["c_free"]
        )

        # check-boxes / text
        sel["low_storage"] = st.sidebar.checkbox(
            f"Low storage (<{Config.LOW_STORAGE_THRESHOLD_GB} GB)",
            value=sel.get("low_storage", False),
        )
        sel["search"] = st.sidebar.text_input(
            "Search", value=sel.get("search", "")
        ).strip()

        return sel

    # ╭──────────────────────────────────────────────────────────╮
    # │ 5.3  FILTER LOGIC                                        │
    # ╰──────────────────────────────────────────────────────────╯
    def _apply_filters(self, assets: Dict[str, dict], f: dict) -> Dict[str, dict]:
        out: Dict[str, dict] = {}
        for name, a in assets.items():
            if f["os"] and self._norm_os(a.get("os_info", {}).get("version")) not in f["os"]:
                continue
            if (
                f["manufacturer"]
                and a.get("system_info", {}).get("manufacturer", "Unknown")
                not in f["manufacturer"]
            ):
                continue
            ram = a.get("hardware_info", {}).get("memory", {}).get("total_gb", 0)
            if not (f["ram"][0] <= ram <= f["ram"][1]):
                continue
            c_free = self._c_drive_free(a) or 0.0
            if not (f["c_free"][0] <= c_free <= f["c_free"][1]):
                continue
            if f["low_storage"] and c_free >= Config.LOW_STORAGE_THRESHOLD_GB:
                continue
            if f["search"] and f["search"].lower() not in json.dumps(a).lower():
                continue
            out[name] = a
        return out

    # ╭──────────────────────────────────────────────────────────╮
    # │ 5.4  VARIOUS SMALL HELPERS                               │
    # ╰──────────────────────────────────────────────────────────╯
    @staticmethod
    def _norm_os(os_string: str | None) -> str:
        if not os_string:
            return "Unknown"
        os_l = os_string.lower()
        mapping = {
            "windows 11": "Windows 11",
            "windows 10": "Windows 10",
            "windows 8": "Windows 8",
            "windows 7": "Windows 7",
            "windows server 2022": "Windows Server 2022",
            "windows server 2019": "Windows Server 2019",
            "windows server 2016": "Windows Server 2016",
            "windows server": "Windows Server",
        }
        for k, v in mapping.items():
            if k in os_l:
                return v
        return os_string

    @staticmethod
    def _c_drive_free(asset: dict) -> Optional[float]:
        """Return C: free space (GB) if known."""
        # assume parser already put in structured location
        for part in asset.get("hardware_info", {}).get("storage", []):
            name = part.get("name", "").upper()
            if "C:" in name or "C DRIVE" in name:
                return part.get("free_space_gb")
        # Fallback regex
        raw = asset.get("raw_content", "")
        m = re.search(r"C:.*?(\d+(?:\.\d+)?)\s*GB.*?free", raw, re.I)
        if m:
            return float(m.group(1))
        return None

    # ╭──────────────────────────────────────────────────────────╮
    # │ 5.5  RENDERING                                           │
    # ╰──────────────────────────────────────────────────────────╯
    def _header(self) -> None:
        col1, col2, col3 = st.columns([3, 1, 1])

        with col1:
            st.title("🖥️  IT Asset Management Dashboard")
            ts = st.session_state["last_refresh"]
            if ts:
                st.caption(f"Last refresh: {ts:%Y-%m-%d %H:%M:%S}")

        with col2:
            if st.button(f"{Config.THEME_TOGGLE_EMOJI} Theme"):
                st.session_state["theme_mode"] = (
                    "dark" if st.session_state["theme_mode"] == "light" else "light"
                )
                st.experimental_rerun()

        with col3:
            if st.button("🔄 Refresh"):
                self.refresh_data()
                st.experimental_rerun()

    def _overview_metrics(self, assets: Dict[str, dict]) -> None:
        """simple KPIs"""
        total = len(assets)
        online = sum(
            1
            for a in assets.values()
            if a.get("network_info", {}).get("status") == AssetStatus.ONLINE.value
        )
        ram_tot = sum(
            a.get("hardware_info", {}).get("memory", {}).get("total_gb", 0)
            for a in assets.values()
        )
        sto_tot = sum(
            p.get("size_gb", 0)
            for a in assets.values()
            for p in a.get("hardware_info", {}).get("storage", [])
        )

        col1, col2, col3, col4 = st.columns(4)
        col1.metric("Assets", total)
        col2.metric("Online", online, delta=f"{online}/{total}")
        col3.metric("Total RAM", f"{ram_tot:.0f} GB")
        col4.metric("Total storage", f"{sto_tot:.0f} GB")

    def _status_pie(self, assets: Dict[str, dict]) -> None:
        status_count: Dict[str, int] = {}
        for a in assets.values():
            s = a.get("network_info", {}).get("status", AssetStatus.UNKNOWN.value)
            status_count[s] = status_count.get(s, 0) + 1
        if not status_count:
            return
        fig = px.pie(
            names=list(status_count.keys()),
            values=list(status_count.values()),
            title="Status distribution",
        )
        st.plotly_chart(fig, use_container_width=True)

    def _details_table(self, assets: Dict[str, dict]) -> None:
        if not assets:
            st.info("No assets to show.")
            return
        rows: List[dict] = []
        for name, a in assets.items():
            rows.append(
                {
                    "Computer": name,
                    "IP": a.get("network_info", {}).get("ip_address"),
                    "OS": a.get("os_info", {}).get("version"),
                    "Status": a.get("network_info", {}).get("status"),
                    "Manufacturer": a.get("system_info", {}).get("manufacturer"),
                    "Model": a.get("system_info", {}).get("model"),
                    "RAM (GB)": a.get("hardware_info", {}).get("memory", {}).get(
                        "total_gb"
                    ),
                }
            )
        df = pd.DataFrame(rows)
        csv = df.to_csv(index=False).encode()
        st.download_button(
            "📥 Download CSV",
            data=csv,
            mime="text/csv",
            file_name=f"asset_report_{datetime.now():%Y%m%d_%H%M%S}.csv",
        )
        st.dataframe(df, use_container_width=True)

    # ╭──────────────────────────────────────────────────────────╮
    # │ 5.6  MAIN ENTRY                                          │
    # ╰──────────────────────────────────────────────────────────╯
    def run(self) -> None:
        apply_windows11_theme()

        # ensure we have data at least once
        if not st.session_state["assets_data"]:
            self.refresh_data()

        self._header()

        filters = self.sidebar()
        filtered_assets = self._apply_filters(st.session_state["assets_data"], filters)

        # ----- body -----
        self._overview_metrics(filtered_assets)
        st.divider()
        self._status_pie(filtered_assets)
        st.divider()
        self._details_table(filtered_assets)


# ╭──────────────────────────────────────────────────────────────╮
# │ 6.  SCRIPT MAIN                                             │
# ╰──────────────────────────────────────────────────────────────╯
if __name__ == "__main__":
    ITAssetDashboard().run()