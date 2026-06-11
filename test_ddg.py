import urllib.request
import urllib.parse
import re
import json

def get_ddg_image(query):
    headers = {
        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36'
    }
    # Step 1: Get VQD token
    url = f"https://duckduckgo.com/?q={urllib.parse.quote(query)}"
    req = urllib.request.Request(url, headers=headers)
    try:
        with urllib.request.urlopen(req) as response:
            html = response.read().decode('utf-8')
        
        # Regex to find vqd
        vqd_match = re.search(r"vqd=([\d-]+)", html)
        if not vqd_match:
            # try finding in other formats
            vqd_match = re.search(r"vqd\s*:\s*['\"]([^'\"]+)['\"]", html)
        
        if not vqd_match:
            print("Failed to find VQD token")
            return None
        
        vqd = vqd_match.group(1)
        print("Found VQD:", vqd)
        
        # Step 2: Fetch images JSON
        img_url = f"https://duckduckgo.com/i.js?o=json&q={urllib.parse.quote(query)}&vqd={vqd}"
        img_req = urllib.request.Request(img_url, headers=headers)
        with urllib.request.urlopen(img_req) as img_response:
            data = json.loads(img_response.read().decode('utf-8'))
            
        if 'results' in data and len(data['results']) > 0:
            for result in data['results']:
                if 'image' in result:
                    return result['image']
    except Exception as e:
        print("Error:", e)
    return None

if __name__ == "__main__":
    img = get_ddg_image("Panadol Extra")
    print("Image URL:", img)
