# ロードバランサー 管理コンソール (HAProxy + Keepalived)

本プロジェクトは、HAProxy および Keepalived を用いて構築されたロードバランサーの運用・管理を、ブラウザベースで容易に行うための Web 管理コンソールです。
直感的な UI から、サーバーの切り離し、フェイルオーバー制御、SSL 証明書の更新、設定ファイルのバックアップ・リストアなどを実行できます。

## ネットワーク構成

本システムは、以下の3つのネットワークセグメントを利用して構成されています。

*   **WAN側 (外部ネットワーク): `192.168.1.0/24`**
    *   クライアントからのインバウンド通信を受け付けるセグメントです。
    *   Keepalived によって管理される仮想IP (VIP) がこのネットワーク上に割り当てられます。
*   **LAN側 (内部ネットワーク): `192.168.2.0/24`**
    *   振り分け先となるバックエンドサーバー群 (WebサーバーやAPサーバーなど) が配置されるセグメントです。
    *   ロードバランサーは、HAProxy を通じてこのネットワークへトラフィックを中継(SNAT等含む)します。
*   **ロードバランサー冗長化側: `10.0.0.0/24`**
    *   ロードバランサー（MASTERノードとBACKUPノード）間のヘルスチェックや、Keepalived の VRRP (Virtual Router Redundancy Protocol) 通信専用のセグメントです。

## 主な機能

*   **ダッシュボード**
    *   Keepalived と HAProxy の稼働状態、およびサーバーの稼働時間(Uptime)の確認。
    *   システム・ネットワーク設定ファイル（`haproxy.cfg`, `keepalived.conf`, `01-netcfg.yaml`, `sysctl.conf`, `rules.v4`）と管理プログラムのワンクリックダウンロード。
    *   お手元の `haproxy.cfg` をアップロードしての本番環境リストア機能。
*   **サーバー管理 (ON / OFF)**
    *   バックエンドの各サーバーをプールから動的に切り離し (Disable) / 組み込み (Enable) する機能。変更は HAProxy を無停止でリロードし適用されます。
*   **フェイルオーバー制御**
    *   Keepalived プロセスの起動・停止を UI から制御し、MASTER / BACKUP ノード間で仮想IPを強制的に引き継がせる（手動フェイルオーバー）ことが可能です。
*   **SSL証明書の更新**
    *   `cert+fullchain.pem`（サーバー証明書＋中間証明書）と `privkey.pem`（秘密鍵）の2ファイルをアップロードするだけで、HAProxy 用に自動結合・文法検証を行い、無停止で証明書を更新します。
    *   直前の証明書へのロールバック機能も備えています。
*   **ログと履歴管理**
    *   HAProxy 設定の変更履歴 (内部の Git リポジトリと連携した `git diff` 情報) の閲覧。
    *   Keepalived および HAProxy の直近のシステムログ (`journalctl`) の表示。

## Ubuntu 26.04 向け初期セットアップ例

以下は、本システムを新規の Ubuntu 26.04 サーバーに構築する際の基本的なセットアップ例です。

### 1. 前提となる IP アドレス設定例

*   **仮想IP (WAN VIP):** `192.168.1.90`
*   **MASTER ノード:**
    *   WANインターフェース (例: eth0): `192.168.1.11`
    *   LANインターフェース (例: eth1): `192.168.2.11`
    *   冗長化インターフェース (例: eth2): `10.0.0.11`
*   **BACKUP ノード:**
    *   WANインターフェース (例: eth0): `192.168.1.12`
    *   LANインターフェース (例: eth1): `192.168.2.12`
    *   冗長化インターフェース (例: eth2): `10.0.0.12`

### 2. パッケージのインストール

```bash
sudo apt update
sudo apt install -y haproxy keepalived git iptables python3
```

### 3. Keepalived の設定例 (MASTERノード)

設定ファイル: `/etc/keepalived/keepalived.conf`

```text
vrrp_instance VI_1 {
    state MASTER
    interface eth0             # WAN側のインターフェースを指定
    virtual_router_id 51
    priority 100               # BACKUPノードは 90 など低い値を設定
    advert_int 1
    
    # 冗長化ネットワーク経由でVRRPパケットをやり取りする場合の指定
    unicast_src_ip 10.0.0.11
    unicast_peer {
        10.0.0.12
    }

    virtual_ipaddress {
        192.168.1.90/24        # WAN側のVIP
    }
}
```

### 4. サービスの起動と有効化

```bash
sudo systemctl enable --now keepalived
sudo systemctl enable --now haproxy
```

### 5. 管理コンソールの配置と起動

1. `/opt/lb_admin/` ディレクトリを作成し、`lb_admin.py` と `index.html` を配置します。
2. 必要に応じて `lb_admin.py` 内の Basic認証情報 (`AUTH_USER`, `AUTH_PASS`) を変更します。
3. `lb_admin.py` に実行権限を付与します。
    ```bash
    sudo chmod +x /opt/lb_admin/lb_admin.py
    ```
4. **systemd にサービスとして登録し、自動起動を設定します。**

   以下の内容で `/etc/systemd/system/lb-admin.service` ファイルを新規作成します。

   ```ini
   [Unit]
   Description=Load Balancer Web Admin Console
   After=network.target

   [Service]
   Type=simple
   User=root
   WorkingDirectory=/opt/lb_admin
   ExecStart=/usr/bin/python3 /opt/lb_admin/lb_admin.py
   Restart=always

   [Install]
   WantedBy=multi-user.target
   ```

5. systemd をリロードし、サービスの有効化と起動を行います。

   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable --now lb-admin
   ```

6. サービスが正常に起動しているか確認します。

   ```bash
   sudo systemctl status lb-admin
   ```
   ブラウザから `http://<ロードバランサーのIP>:8080/` にアクセスし、管理コンソール画面が表示されればセットアップは完了です。