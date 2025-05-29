# CED Asset Manager & Dashboard

This application is a Python script that parses asset information from text files, provides a web-based dashboard using Gradio for filtering and viewing asset details, and visualizes system statistics.

## Features

*   **Automatic Dependency Installation:** Checks for required Python libraries (`pandas`, `gradio`, `matplotlib`, `plotly`) on the first run and installs them automatically using `pip`.
*   **Asset File Parsing:** Reads and extracts structured asset information from `.txt` files located in the `attached_assets` directory.
*   **Web Dashboard:** Provides a user-friendly interface via Gradio to:
    *   View a list of assets with key information.
    *   Filter assets by Hostname, OS, Manufacturer, and RAM range.
    *   View detailed information for a selected asset in a popup modal.
*   **System Statistics:** Displays pie charts showing the distribution of Operating Systems and System Manufacturers among the parsed assets.

## Requirements

*   Python 3.6+
*   Internet connection (for dependency installation on first run)

## Setup and Running

1.  **Clone or download** this repository to your local machine.
2.  **Place your asset text files** inside the `attached_assets` directory. The script expects files named in the format `IP_Hostname.txt`.
3.  **Run the main script:**
    ```bash
    python main.py
    ```
4.  **First Run:** The script will automatically check for and install the necessary dependencies. This may take a few minutes depending on your internet connection.
5.  **Access the Dashboard:** Once the script is running, open your web browser and go to the address provided in the terminal output (usually `http://0.0.0.0:7867`).

## Asset File Format

The script expects text files in the `attached_assets` directory. Each file should contain asset information with key-value pairs that the script's regex patterns can match. The filename should follow the `IP_Hostname.txt` format (e.g., `192.168.1.100_MYPC.txt`).

The script attempts to extract the following information using regex:

*   OS Version
*   User Email(s)
*   CPU
*   RAM
*   GPU
*   System Manufacturer
*   System Model
*   BIOS Version
*   Windows Language
*   Antivirus
*   Office Version
*   OS Activation
*   Local Disks (parsed as a block)

Ensure your asset files contain this information in a consistent format for the script to parse it correctly.

## Dependencies

The required Python packages are listed in `requirements.txt`:

*   pandas
*   gradio
*   matplotlib
*   plotly

These will be automatically installed on the first run of `main.py`.

## Contributing

(Add contributing guidelines here if applicable)

## License

(Add license information here if applicable)
