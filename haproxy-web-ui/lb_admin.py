#!/usr/bin/env python3
import http.server
import socketserver
import subprocess
import json
import os
import base64
import shutil
import datetime
from urllib.parse import urlparse, parse_qs

# ==========================================
# 認証情報 (Basic認証) - 運用時は適宜変更してください
# ==========================================
AUTH_USER = "admin"
AUTH_PASS = "admin123"

# 各種パス設定
PORT = 8080
CERT_PATH = "/etc/haproxy/certs/fullchain_and_key.pem"
CERT_BAK_PATH = "/etc/haproxy/certs/fullchain_and_key.pem.bak"
CERT_TEMP_PATH = "/etc/haproxy/certs/temp.pem"
CFG_PATH = "/etc/haproxy/haproxy.cfg"
TEMP_CFG_PATH = "/etc/haproxy/haproxy_temp.cfg"
HAPROXY_DIR = "/etc/haproxy"
BACKUP_DIR = "/etc/haproxy/backup"

def init_git_and_dirs():
    """Gitリポジトリとバックアップディレクトリの初期化"""
    os.makedirs(HAPROXY_DIR, exist_ok=True)
    os.makedirs(BACKUP_DIR, exist_ok=True)
    
    git_dir = os.path.join(HAPROXY_DIR, ".git")
    if not os.path.exists(git_dir):
        subprocess.run(["git", "init"], cwd=HAPROXY_DIR, stdout=subprocess.DEVNULL)
        subprocess.run(["git", "config", "user.name", "LB WebAdmin"], cwd=HAPROXY_DIR)
        subprocess.run(["git", "config", "user.email", "admin@localhost"], cwd=HAPROXY_DIR)
        if os.path.exists(CFG_PATH):
            subprocess.run(["git", "add", "haproxy.cfg"], cwd=HAPROXY_DIR, stdout=subprocess.DEVNULL)
            subprocess.run(["git", "commit", "-m", "Initial commit"], cwd=HAPROXY_DIR, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

def create_timestamp_backup(file_path):
    """ファイルを書き換える前に、必ずタイムスタンプ付きのバックアップを作成する"""
    if os.path.exists(file_path):
        os.makedirs(BACKUP_DIR, exist_ok=True)
        timestamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
        filename = os.path.basename(file_path)
        backup_path = os.path.join(BACKUP_DIR, f"{filename}.{timestamp}.bak")
        shutil.copy2(file_path, backup_path)
        return backup_path
    return None

def get_node_ips():
    try:
        out = subprocess.getoutput("ip -4 addr show")
        ips = []
        for line in out.split("\n"):
            if "inet " in line:
                ip = line.split().split("/")
                if ip != "127.0.0.1":
                    ips.append(ip)
        return ips
    except Exception:
        return []

def get_servers():
    if not os.path.exists(CFG_PATH): return []
    with open(CFG_PATH, "r") as f: lines = f.readlines()
    
    servers = []
    curr_backend = ""
    for i, line in enumerate(lines):
        s = line.strip()
        if s.startswith("backend "):
            curr_backend = s.split() if len(s.split()) > 1 else ""
        elif s.startswith("server "):
            parts = s.split()
            if len(parts) >= 3:
                srv_name = parts
                ip_port = parts
                if ':' in ip_port:
                    ip, port = ip_port.split(':', 1)
                else:
                    ip, port = ip_port, ""
            else:
                srv_name = "unknown"
                ip = ""
                port = ""
                
            servers.append({
                "index": i,
                "backend": curr_backend,
                "name": srv_name,
                "ip": ip,
                "port": port,
                "disabled": "disabled" in parts
            })
    return servers

def get_cert_expiry(path):
    if not os.path.exists(path):
        return "証明書ファイルが存在しません"
    res = subprocess.run(["openssl", "x509", "-enddate", "-noout", "-in", path], capture_output=True, text=True)
    if res.returncode == 0:
        return res.stdout.strip().replace("notAfter=", "")
    return "読み取りエラー (形式異常など)"

class ApiHandler(http.server.BaseHTTPRequestHandler):
    def check_auth(self):
        auth_header = self.headers.get('Authorization')
        if not auth_header or not auth_header.startswith('Basic '): return False
        try:
            encoded = auth_header.split(' ')
            decoded = base64.b64decode(encoded).decode('utf-8')
            u, p = decoded.split(':', 1)
            return u == AUTH_USER and p == AUTH_PASS
        except Exception:
            return False

    def require_auth(self):
        self.send_response(401)
        self.send_header('WWW-Authenticate', 'Basic realm="LB Admin Console"')
        self.end_headers()
        self.wfile.write(b"Unauthorized")

    def send_json(self, status_code, data):
        self.send_response(status_code)
        self.send_header('Content-Type', 'application/json; charset=utf-8')
        self.end_headers()
        self.wfile.write(json.dumps(data).encode('utf-8'))

    # ==================================================
    # GET リクエスト
    # ==================================================
    def do_GET(self):
        if not self.check_auth(): return self.require_auth()

        if self.path == '/':
            html_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "index.html")
            try:
                with open(html_path, "r", encoding="utf-8") as f: content = f.read()
                self.send_response(200)
                self.send_header('Content-type', 'text/html; charset=utf-8')
                self.end_headers()
                self.wfile.write(content.encode('utf-8'))
            except FileNotFoundError:
                self.send_response(404)
                self.end_headers()
                self.wfile.write(b"index.html not found")
            return

        elif self.path == '/api/status':
            data = {
                "keepalived": subprocess.getoutput("systemctl is-active keepalived"),
                "haproxy": subprocess.getoutput("systemctl is-active haproxy"),
                "uptime": subprocess.getoutput("uptime -p").replace("up ", ""),
                "node_ips": get_node_ips(),
                "servers": get_servers(),
                "cert_expiry": get_cert_expiry(CERT_PATH),
                "has_backup": os.path.exists(CERT_BAK_PATH),
                "git_log": subprocess.getoutput(f"cd {HAPROXY_DIR} && git log -p -n 3 haproxy.cfg"),
                "sys_log": subprocess.getoutput("journalctl -u keepalived -u haproxy -n 30 --no-pager")
            }
            self.send_json(200, data)
            return

        # 指定されたファイルをダウンロードするAPI
        elif self.path.startswith('/api/download'):
            query = parse_qs(urlparse(self.path).query)
            file_type = query.get('type', [''])
            
            # ダウンロードを許可するファイルのホワイトリスト (追加ファイル対応)
            allowed_files = {
                'haproxy': ('/etc/haproxy/haproxy.cfg', 'haproxy.cfg'),
                'keepalived': ('/etc/keepalived/keepalived.conf', 'keepalived.conf'),
                'netplan': ('/etc/netplan/01-netcfg.yaml', '01-netcfg.yaml'),
                'sysctl': ('/etc/sysctl.conf', 'sysctl.conf'),
                'iptables': ('/etc/iptables/rules.v4', 'rules.v4'),
                'lb_admin': ('/opt/lb_admin/lb_admin.py', 'lb_admin.py'),
                'index': ('/opt/lb_admin/index.html', 'index.html')
            }
            
            if file_type in allowed_files:
                filepath, filename = allowed_files[file_type]
                try:
                    with open(filepath, "rb") as f: content = f.read()
                    self.send_response(200)
                    self.send_header('Content-type', 'application/octet-stream')
                    self.send_header('Content-Disposition', f'attachment; filename="{filename}"')
                    self.end_headers()
                    self.wfile.write(content)
                except Exception as e:
                    self.send_response(404)
                    self.end_headers()
                    self.wfile.write(f"Error: {filepath} not found or permission denied.".encode('utf-8'))
            else:
                self.send_response(404)
                self.end_headers()
                self.wfile.write(b"File not found or access denied.")
            return

        self.send_response(404)
        self.end_headers()

    # ==================================================
    # POST リクエスト (設定変更)
    # ==================================================
    def do_POST(self):
        if not self.check_auth(): return self.require_auth()

        length = int(self.headers.get('Content-Length', 0))
        try: params = json.loads(self.rfile.read(length).decode('utf-8'))
        except: params = {}

        if self.path == '/api/action':
            cmd = params.get('cmd')
            if cmd == 'stop_keepalived': subprocess.run(["systemctl", "stop", "keepalived"])
            elif cmd == 'start_keepalived': subprocess.run(["systemctl", "start", "keepalived"])
            self.send_json(200, {"message": "Keepalivedの操作を完了しました。"})
            return

        elif self.path == '/api/toggle_server':
            idx = params.get('index')
            if idx is not None and os.path.exists(CFG_PATH):
                create_timestamp_backup(CFG_PATH)
                shutil.copy2(CFG_PATH, CFG_PATH + ".bak")
                
                with open(CFG_PATH, "r") as f: lines = f.readlines()
                line = lines[idx].rstrip("\n")
                
                if " disabled" in line:
                    lines[idx] = line.replace(" disabled", "") + "\n"
                    action_msg = "Enable"
                else:
                    lines[idx] = line + " disabled\n"
                    action_msg = "Disable"
                
                srv_name = line.strip().split() if len(line.strip().split()) > 1 else "server"
                with open(CFG_PATH, "w") as f: f.writelines(lines)
                
                res = subprocess.run(["haproxy", "-c", "-f", CFG_PATH], capture_output=True, text=True)
                if res.returncode == 0:
                    subprocess.run(["git", "add", "haproxy.cfg"], cwd=HAPROXY_DIR)
                    subprocess.run(["git", "commit", "-m", f"WebUI: {action_msg} server {srv_name}"], cwd=HAPROXY_DIR)
                    subprocess.run(["systemctl", "reload", "haproxy"])
                    self.send_json(200, {"message": f"{srv_name} を {action_msg} にしました。"})
                else:
                    shutil.copy2(CFG_PATH + ".bak", CFG_PATH)
                    self.send_json(400, {"message": f"検証エラーにより変更を破棄しました。\n\n{res.stderr}"})
            return

        elif self.path == '/api/restore_config':
            config_data = params.get('config_data', '')
            if not config_data:
                self.send_json(400, {"message": "設定データが空です。"})
                return

            with open(TEMP_CFG_PATH, "w") as f: f.write(config_data)
            res = subprocess.run(["haproxy", "-c", "-f", TEMP_CFG_PATH], capture_output=True, text=True)
            
            if res.returncode == 0:
                create_timestamp_backup(CFG_PATH)
                shutil.copy2(CFG_PATH, CFG_PATH + ".bak")
                
                shutil.copy2(TEMP_CFG_PATH, CFG_PATH)
                os.remove(TEMP_CFG_PATH)
                
                subprocess.run(["git", "add", "haproxy.cfg"], cwd=HAPROXY_DIR)
                subprocess.run(["git", "commit", "-m", "WebUI: Restore configuration from uploaded file"], cwd=HAPROXY_DIR)
                subprocess.run(["systemctl", "reload", "haproxy"])
                self.send_json(200, {"message": "✅ リストアに成功し、HAProxyを無停止でリロードしました。"})
            else:
                os.remove(TEMP_CFG_PATH)
                self.send_json(400, {"message": f"❌ 設定の文法エラーを検知したため、リストアを中止しました。\n\n{res.stderr}"})
            return

        elif self.path == '/api/verify_cert':
            cert_data = params.get('cert_data', '')
            os.makedirs(os.path.dirname(CERT_PATH), exist_ok=True)
            
            with open(CERT_TEMP_PATH, 'w') as f: f.write(cert_data)
                
            with open(CFG_PATH, 'r') as f: cfg = f.read()
            cfg = cfg.replace(CERT_PATH, CERT_TEMP_PATH)
            with open(TEMP_CFG_PATH, 'w') as f: f.write(cfg)
            
            res = subprocess.run(["haproxy", "-c", "-f", TEMP_CFG_PATH], capture_output=True, text=True)
            if os.path.exists(TEMP_CFG_PATH): os.remove(TEMP_CFG_PATH)
            
            if res.returncode == 0:
                self.send_json(200, {"message": "✅ 検証に成功しました。文法は正常です。\n\n" + res.stdout})
            else:
                self.send_json(400, {"message": "❌ 検証エラーが発生しました。\n\n" + res.stderr})
            return

        elif self.path == '/api/apply_cert':
            if not os.path.exists(CERT_TEMP_PATH):
                self.send_json(400, {"message": "検証済みのデータが見つかりません。"})
                return
                
            if os.path.exists(CERT_PATH):
                create_timestamp_backup(CERT_PATH)
                shutil.copy2(CERT_PATH, CERT_BAK_PATH)
            
            shutil.copy2(CERT_TEMP_PATH, CERT_PATH)
            os.remove(CERT_TEMP_PATH)
            
            subprocess.run(["systemctl", "reload", "haproxy"])
            self.send_json(200, {"message": "証明書を更新し、HAProxyをリロードしました。"})
            return

        elif self.path == '/api/rollback_cert':
            if not os.path.exists(CERT_BAK_PATH):
                self.send_json(400, {"message": "バックアップ証明書が存在しません。"})
                return
            
            create_timestamp_backup(CERT_PATH)
            shutil.copy2(CERT_BAK_PATH, CERT_PATH)
            subprocess.run(["systemctl", "reload", "haproxy"])
            self.send_json(200, {"message": "1つ前の証明書にロールバックしました。"})
            return

        self.send_response(404)
        self.end_headers()

if __name__ == "__main__":
    init_git_and_dirs()
    with socketserver.TCPServer(("0.0.0.0", PORT), ApiHandler) as httpd:
        print(f"API Server started on port {PORT}")
        httpd.serve_forever()