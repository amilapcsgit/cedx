import time
import sys
from asset_parser import AssetParser
from pathlib import Path

def test():
    p = AssetParser()
    files = list(Path('assets').glob('*.txt'))
    print(f"Found {len(files)} files.")
    
    for idx, f in enumerate(files):
        print(f"--- Testing {f.name} ---")
        content = f.read_text(errors='replace')
        sys.stdout.flush()
        
        # Test basic extract fields
        for k in p.patterns:
            t0 = time.time()
            p.extract_field(content, k)
            if time.time() - t0 > 0.5:
                print(f"HANG IN PATTERN: {k} took {time.time() - t0:.2f}s")
                sys.stdout.flush()
                
        # Test specific parsers
        t0 = time.time()
        p.parse_memory_size('16 GB')
        if time.time() - t0 > 0.5: print("HANG IN memory")
        
        t0 = time.time()
        p.parse_storage_info(content)
        if time.time() - t0 > 0.5: print("HANG IN storage")
        
        t0 = time.time()
        p.parse_network_info(content)
        if time.time() - t0 > 0.5: print("HANG IN network")
        
        t0 = time.time()
        p.parse_software_list(content)
        if time.time() - t0 > 0.5: print("HANG IN software")
        
        print("Done file.")
        sys.stdout.flush()

if __name__ == '__main__':
    test()
