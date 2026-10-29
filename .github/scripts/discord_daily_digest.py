#!/usr/bin/env python3
"""直近 N 時間に develop へマージされた PR の機能紹介を Discord 進捗チャンネルへ投稿する。

discord_daily_digest.yml から呼ばれる。設定はすべて環境変数で受け取る(ユーザー入力をシェルに展開しないため)。
  GH_TOKEN, REPO, DISCORD_PROGRESS_WEBHOOK_URL, HOURS, DRY_RUN, PR_NUMBER, DIGEST_FORMAT
PR_NUMBER(任意): 指定すると期間・マージ状態・base・Bot除外の絞り込みを無視し、その PR 1件だけを
  通常と同じ形式で投稿する(未マージでも可。ローカルでのテスト投稿用)。DRY_RUN と併用できる。
  カンマ区切りで複数指定可(例: 53,54,55)。指定順に取得し、同じ同梱ロジックで1つの投稿にまとめる。
Webhook URL はログに出さない。
対象期間と見出し日付: HOURS が空(スケジュール実行)のときは「直近の21:00 JST(DIGEST_HOUR_JST)」を end とし、
  end-24h < mergedAt <= end の PR を対象、見出し日付を end の JST 日付にする(半開区間なので前後の実行と重複・欠落しない)。
  実行時刻(now)ではなく固定の21:00を基準にするため、cron が遅延しても期間と見出し日付がずれない。
  HOURS が指定されたとき(手動実行)は従来どおり now から HOURS 時間さかのぼり、見出し日付は now の JST 日付。

投稿形式(DIGEST_FORMAT、既定 card): card は PR ごとのカードを上限内で1メッセージに同梱する。
  card: Discord Components V2。PR ごとの Container(カード)を、1メッセージあたり Text Display 合計 3800 文字・
    添付 10 個・コンポーネント 40 個の範囲でマージ順に貪欲に詰め、超える場合は新しいメッセージに分ける。
    日付見出しは最初のメッセージ先頭の Text Display(Container の外)に入れる。
    1PR のカードは必ず1メッセージに収まる(概要を削る)。flags=32768(IS_COMPONENTS_V2)を付け、content / embeds は使わない。
    1つの Container(左に色の帯が付くカード)に 見出しリンク+作成者 → 概要 → 動画(Media Gallery) の順で入れる。
    送信URLに ?with_components=true を付ける。動画は attachment://videoN.ext で参照する。
    動画リンク(添付できなかった動画、圧縮して添付した場合の元動画)は、概要と同じ Text Display の末尾
    (概要の下)に `🎬 [実行動画を見る](<URL>)` / `🎬 [元の動画](<URL>)` として置く(複数なら末尾に連番)。
    全 Text Display の合計は 3800 文字以内(概要を削り、見出し・作成者・動画リンクを優先して残す)。
  text: 従来形式(1PR = 1メッセージ)。content(見出し→作成者→概要→動画リンク、2000文字以内)に動画を添付する。
    Components V2 が Webhook で受理されない・動画が再生されない場合の切り替え用。
動画: user-attachments の URL は先頭だけ取得して Content-Type が video/* かを判定し(貼られたセクションは問わない)、
  10MB 以下なら添付。超過した動画は ffmpeg/ffprobe があれば再エンコードして約 9MB 以下にして添付し(元動画はリンクを添える)、
  ffmpeg が無い・圧縮失敗・圧縮後も超過・ダウンロード失敗ならカード内にリンクで出す。各動画の扱いと理由はログに出す。
見出し記法(card / text 共通): 日付メッセージ=H1(`# `)、PR タイトルのリンク=H2(`## `)、概要=H3(`### `)。
text 形式の日付見出しは通常の content メッセージ。allowed_mentions は常に parse=[]。
"""
import datetime
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time

# Discord の無料枠の添付上限は 10MB。ペイロード分の余裕を見て少し小さくしておく。
MAX_VIDEO_BYTES = 10_000_000
MAX_VIDEOS_PER_PR = 3
# 1PR で調べる動画 URL の上限と、リンク行として出す最大行数(超過分は「他N件」にまとめる)。
MAX_VIDEO_CANDIDATES = 6
MAX_LINK_LINES = 5
# 1メッセージに同梱する添付ファイルの合計サイズ上限。
MAX_MESSAGE_ATTACH_BYTES = 20_000_000
# 圧縮の元動画としてダウンロードする上限と、そのダウンロードの curl タイムアウト(秒)。
COMPRESS_SRC_MAX_BYTES = 200_000_000
COMPRESS_DOWNLOAD_TIMEOUT = 600
# 圧縮後の目標合計サイズ(音声込み)。コンテナの余裕として SAFETY を掛けて映像ビットレートを逆算する。
COMPRESS_TARGET_BYTES = 9_000_000
COMPRESS_AUDIO_BPS = 64_000
COMPRESS_SAFETY = 0.9
COMPRESS_MIN_VIDEO_BPS = 100_000
COMPRESS_TIMEOUT = 300
# content の上限は 2000 文字。安全側で短くする。
CONTENT_LIMIT = 1900
# Components V2 の Text Display 合計上限は 4000 文字。安全側で短くする。
CARD_TEXT_LIMIT = 3800
MAX_MESSAGE_ATTACHMENTS = 10
# Components V2 のメッセージあたりコンポーネント上限は 40(Container・Text Display・Media Gallery とそのアイテムを数える)。
MAX_MESSAGE_COMPONENTS = 40
CARD_COLOR = 0x5865F2
IS_COMPONENTS_V2 = 1 << 15
JST = datetime.timezone(datetime.timedelta(hours=9))
# スケジュール実行の期間の終端(JST の時)。
DIGEST_HOUR_JST = 21

URL_CHARS = r"[^\s)\"'<>\]]"
ASSET_RE = re.compile(r"https://github\.com/user-attachments/(?:assets|files)/" + URL_CHARS + "+")
EXT_RE = re.compile(r"https?://" + URL_CHARS + r"+?\.(?:mp4|mov|webm)(?:\?" + URL_CHARS + "*)?", re.IGNORECASE)


def strip_comments(text):
    return re.sub(r"<!--.*?-->", "", text.replace("\r", ""), flags=re.DOTALL)


def get_section(body, heading_prefix):
    """`## <heading_prefix>...` から次の `## ` 見出しまでを返す。無ければ None。"""
    out, inside = [], False
    for line in body.split("\n"):
        if re.match(r"^##\s", line):
            if inside:
                break
            inside = line[3:].strip().startswith(heading_prefix)
            continue
        if inside:
            out.append(line)
    return "\n".join(out) if inside else None


def clean_section(text):
    """見出しを太字化し、空の箇条書き・空白を整理する。実質空なら空文字を返す。"""
    if not text:
        return ""
    lines = []
    for line in text.split("\n"):
        if re.match(r"^\s*@coderabbitai summary\s*$", line):
            continue
        # 未記入のテンプレ行(「-」「- 」「- [ ]」だけ)は除外
        if re.match(r"^\s*[-*+]\s*(\[[ xX]?\]\s*)?$", line):
            continue
        m = re.match(r"^#{3,6}\s+(.*)$", line)
        if m:
            line = "**" + m.group(1).strip() + "**"
        lines.append(line.rstrip())
    return re.sub(r"\n{3,}", "\n\n", "\n".join(lines)).strip()


def clip(text, limit):
    if len(text) <= limit:
        return text
    head = text[: limit - 1]
    parts = head.split("\n")
    if len(parts) > 1:
        by_line = "\n".join(parts[:-1]).rstrip()
        # 行単位で切ると本文がほとんど残らない(改行の少ない長文)場合は、文字単位で切って本文を残す
        if len(by_line) >= limit / 2:
            return by_line + "…"
    return head.rstrip() + "…"


def build_description(body):
    """「## 概要」の整形済み本文(見出し付き)を返す。空なら空文字を返す。"""
    sec = clean_section(get_section(body, "概要"))
    if not sec:
        return ""
    return "### 概要\n" + sec


def escape_md(text):
    return re.sub(r"([\\[\]*_`~|])", r"\\\1", text.replace("\n", " "))


def build_link_lines(links, originals):
    """動画リンク行を返す。links=添付できなかった動画、originals=圧縮して添付した動画の元URL。
    URL は <> で囲んで埋め込みプレビューを抑止する。複数なら末尾に連番を振る。"""
    lines = []
    for urls, single, multi in ((links, "実行動画を見る", "実行動画"), (originals, "元の動画", "元の動画")):
        for n, u in enumerate(urls, 1):
            lines.append("🎬 [%s](<%s>)" % (single if len(urls) == 1 else multi + str(n), u))
    if len(lines) > MAX_LINK_LINES:
        extra = len(lines) - (MAX_LINK_LINES - 1)
        lines = lines[:MAX_LINK_LINES - 1] + ["🎬 他%d件の動画" % extra]
    return "\n".join(lines)


def build_body(desc, link_lines, budget):
    """概要の下に動画リンク行を置いた本文を返す。budget 文字以内で、リンク行を優先して概要を削る。"""
    if budget <= 0:
        return ""
    link_lines = clip(link_lines, budget)
    room = budget - len(link_lines) - (2 if link_lines else 0)
    text = clip(desc, room) if desc and room > 20 else ""
    out = text + ("\n\n" if text and link_lines else "") + link_lines
    return out if len(out) <= budget else clip(out, budget)


def build_content(pr, login, desc, links, originals=(), limit=CONTENT_LIMIT):
    """1PR 分の本文を組み立てる。limit 文字以内。見出し・作成者・動画リンクを優先し、概要を削る。"""
    title = "#%d %s" % (pr["number"], pr["title"])
    heading = "## [%s](<%s>)" % (escape_md(clip(title, 300)), pr["url"])
    head = heading + "\n作成者: " + login
    body = build_body(desc, build_link_lines(links, originals), limit - len(head) - 2)
    content = head + ("\n\n" + body if body else "")
    return content if len(content) <= limit else clip(content, limit)


def build_card(pr, login, desc, links, originals, media_names, limit=CARD_TEXT_LIMIT):
    """Components V2 の Container を1つ返す。Text Display の合計は limit 文字以内。
    概要とリンク行は同じ Text Display に入れる(コンポーネント数を増やさない)。"""
    title = "#%d %s" % (pr["number"], pr["title"])
    heading = "## [%s](<%s>)" % (escape_md(clip(title, 300)), pr["url"])
    head = heading + "\n作成者: " + login
    parts = [{"type": 10, "content": head}]
    body = build_body(desc, build_link_lines(links, originals), limit - len(head))
    if body:
        parts.append({"type": 10, "content": body})
    if media_names:
        parts.append({"type": 12, "items": [{"media": {"url": "attachment://" + n}} for n in media_names]})
    return {"type": 17, "accent_color": CARD_COLOR, "components": parts}


def count_components(components):
    """トップレベル components 配下のコンポーネント総数(入れ子の子も数える)。"""
    n = 0
    for c in components:
        n += 1
        n += count_components(c.get("components", []))
        n += len(c.get("items", []))
    return n


def text_total(components):
    """Text Display(type 10)の文字数合計。入れ子も数える。"""
    n = 0
    for c in components:
        if c["type"] == 10:
            n += len(c["content"])
        n += text_total(c.get("components", []))
    return n


def pack_messages(items, header, text_limit=CARD_TEXT_LIMIT):
    """items([{pr, login, desc, links, originals, files:[(path, ext, size)]}])を順に貪欲に詰めてメッセージのリストを返す。

    各メッセージ: {components, attach:[(path, name)], text, comps}。header は最初のメッセージ先頭に置く。
    1カードが単独で上限を超えないよう、概要は header 分を含めて text_limit に収まるよう削る。
    """
    per_card_limit = text_limit - len(header)
    msgs = []
    cur = None
    for it in items:
        base = len(cur["attach"]) if cur else 0
        names = ["video%d%s" % (base + n + 1, f[1]) for n, f in enumerate(it["files"])]
        fbytes = sum(f[2] if len(f) > 2 else 0 for f in it["files"])
        card = build_card(it["pr"], it["login"], it["desc"], it["links"], it["originals"], names, limit=per_card_limit)
        fits = False
        if cur is not None:
            fits = (cur["text"] + text_total([card]) <= text_limit
                    and len(cur["attach"]) + len(names) <= MAX_MESSAGE_ATTACHMENTS
                    and cur["bytes"] + fbytes <= MAX_MESSAGE_ATTACH_BYTES
                    and cur["comps"] + count_components([card]) <= MAX_MESSAGE_COMPONENTS)
        if not fits:
            cur = {"components": [], "attach": [], "text": 0, "comps": 0, "bytes": 0}
            if not msgs:
                h = {"type": 10, "content": header}
                cur["components"].append(h)
                cur["text"] += len(header)
                cur["comps"] += 1
            msgs.append(cur)
            # 新メッセージでは添付名を 1 から振り直す
            names = ["video%d%s" % (n + 1, f[1]) for n, f in enumerate(it["files"])]
            card = build_card(it["pr"], it["login"], it["desc"], it["links"], it["originals"], names, limit=per_card_limit)
        cur["components"].append(card)
        cur["attach"] += [(f[0], name) for f, name in zip(it["files"], names)]
        cur["bytes"] += fbytes
        cur["text"] += text_total([card])
        cur["comps"] += count_components([card])
    return msgs


def extract_videos(body):
    """(url, 動画と見なせるか) のリスト。実行動画セクション優先、無ければ本文全体から探す。"""
    def find(text):
        found = []
        for m in list(ASSET_RE.finditer(text)) + list(EXT_RE.finditer(text)):
            u = m.group(0).rstrip(".,;")
            if u not in found:
                found.append(u)
        return found

    sec = get_section(body, "実行動画")
    urls = find(sec) if sec is not None else []
    # セクションが無い・URL が1件も無い(空やテンプレのコメントだけ)ときは本文全体から探す
    in_section = bool(urls)
    if not urls:
        urls = find(body)
    return [(u, in_section or bool(EXT_RE.fullmatch(u))) for u in urls]


def run(cmd):
    return subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")


def probe(url, hdr_path):
    """先頭 100 バイトだけ取得して (ok, content_type, size or None, reason) を返す。トークンは送らない。"""
    r = run(["curl", "-sSL", "--max-time", "30", "-r", "0-99", "-o", os.devnull, "-D", hdr_path,
             "-w", "%{http_code} %{content_type}", url])
    if r.returncode != 0:
        return False, "", None, "probe curl error %d" % r.returncode
    code, _, ctype = r.stdout.strip().partition(" ")
    if code not in ("200", "206"):
        return False, ctype.lower(), None, "probe http " + code
    size = None
    try:
        with open(hdr_path, encoding="utf-8", errors="replace") as f:
            blocks = [b for b in f.read().replace("\r", "").split("\n\n") if b.strip()]
        last = blocks[-1] if blocks else ""  # リダイレクトを辿った最後のレスポンスのヘッダ
        m = re.search(r"^content-range:\s*bytes\s+\d+-\d+/(\d+)", last, re.I | re.M)
        if m:
            size = int(m.group(1))
        elif code == "200":
            m = re.search(r"^content-length:\s*(\d+)", last, re.I | re.M)
            if m:
                size = int(m.group(1))
    except OSError:
        pass
    return True, ctype.lower(), size, ""


def download(url, dest, max_bytes=MAX_VIDEO_BYTES, timeout=120):
    """動画をダウンロードし (ok, reason, content_type) を返す。トークンは送らない。"""
    r = run(["curl", "-sSL", "--max-time", str(timeout), "--max-filesize", str(max_bytes),
             "-o", dest, "-w", "%{http_code} %{content_type}", url])
    if r.returncode != 0:
        return False, "download failed or too large", ""
    code, _, ctype = r.stdout.strip().partition(" ")
    if code != "200":
        return False, "http " + code, ctype.lower()
    if os.path.getsize(dest) > max_bytes:
        return False, "too large", ctype.lower()
    return True, "", ctype.lower()


def calc_video_bitrate(duration, target_bytes=COMPRESS_TARGET_BYTES, audio_bps=COMPRESS_AUDIO_BPS,
                       safety=COMPRESS_SAFETY):
    """長さ(秒)と目標合計サイズ(音声込み)から映像ビットレート(bps)を逆算する。"""
    return int(target_bytes * 8 * safety / duration - audio_bps)


def build_ffmpeg_cmd(src, dst, video_bps):
    """再エンコード用の ffmpeg コマンド引数を返す(実行はしない)。"""
    return ["ffmpeg", "-y", "-i", src,
            "-c:v", "libx264", "-preset", "veryfast",
            "-b:v", str(video_bps), "-maxrate", str(video_bps), "-bufsize", str(video_bps * 2),
            "-vf", "scale='min(1280,iw)':-2", "-pix_fmt", "yuv420p", "-movflags", "+faststart",
            "-c:a", "aac", "-b:a", "%dk" % (COMPRESS_AUDIO_BPS // 1000), dst]


def probe_duration(path):
    """ffprobe で動画の長さ(秒)を返す。取得できなければ None。"""
    r = run(["ffprobe", "-v", "error", "-show_entries", "format=duration",
             "-of", "default=noprint_wrappers=1:nokey=1", path])
    try:
        d = float(r.stdout.strip().splitlines()[0])
    except (ValueError, IndexError):
        return None
    return d if r.returncode == 0 and d > 0 else None


def compress_video(src, dst):
    """src を約 9MB 以下の mp4 に再エンコードして dst に書く。(ok, reason) を返す。"""
    duration = probe_duration(src)
    if duration is None:
        return False, "duration unknown"
    vbps = calc_video_bitrate(duration)
    if vbps < COMPRESS_MIN_VIDEO_BPS:
        return False, "bitrate too low (%d bps for %.0f s)" % (vbps, duration)
    try:
        r = subprocess.run(build_ffmpeg_cmd(src, dst, vbps), capture_output=True, text=True,
                           encoding="utf-8", errors="replace", timeout=COMPRESS_TIMEOUT)
    except subprocess.TimeoutExpired:
        return False, "ffmpeg timeout"
    if r.returncode != 0 or not os.path.exists(dst):
        return False, "ffmpeg error %d" % r.returncode
    if os.path.getsize(dst) > MAX_VIDEO_BYTES:
        return False, "still too large (%d bytes)" % os.path.getsize(dst)
    return True, ""


def video_ext(url, ctype):
    if "quicktime" in ctype:
        return ".mov"
    if "webm" in ctype:
        return ".webm"
    m = re.search(r"\.(mp4|mov|webm)(?:\?|$)", url, re.IGNORECASE)
    return "." + m.group(1).lower() if m else ".mp4"


def post(webhook, payload, files=(), with_components=False):
    cmd = ["curl", "-sS", "--fail-with-body", "-X", "POST", "--form-string",
           "payload_json=" + json.dumps(payload, ensure_ascii=False)]
    for i, (path, name) in enumerate(files):
        cmd += ["-F", "files[%d]=@%s;filename=%s" % (i, path, name)]
    url = webhook
    if with_components:
        url += ("&" if "?" in url else "?") + "with_components=true"
    cmd.append(url)
    for attempt in range(3):
        r = run(cmd)
        if r.returncode == 0:
            return
        if '"retry_after"' in r.stdout and attempt < 2:
            try:
                wait = float(json.loads(r.stdout).get("retry_after", 2))
            except ValueError:
                wait = 2
            time.sleep(min(wait, 30) + 0.5)
            continue
        print("::error::Discord への投稿に失敗しました: " + (r.stdout or r.stderr)[:500])
        sys.exit(1)


def latest_digest_end(now):
    """now 以前で最も近い DIGEST_HOUR_JST:00 (JST)を返す。"""
    local = now.astimezone(JST)
    end = local.replace(hour=DIGEST_HOUR_JST, minute=0, second=0, microsecond=0)
    if end > local:
        end -= datetime.timedelta(days=1)
    return end


def main():
    repo = os.environ["REPO"]
    webhook = os.environ.get("DISCORD_PROGRESS_WEBHOOK_URL", "")
    dry_run = os.environ.get("DRY_RUN", "false").lower() == "true"
    hours_raw = os.environ.get("HOURS", "").strip()
    scheduled = not hours_raw
    if scheduled:
        hours = 24
    else:
        if not re.fullmatch(r"[0-9]+", hours_raw) or int(hours_raw) < 1:
            print("::error::hours は 1 以上の整数で指定してください。")
            sys.exit(1)
        hours = int(hours_raw)
    if not webhook and not dry_run:
        print("::error::Secret DISCORD_PROGRESS_WEBHOOK_URL が未設定です。進捗チャンネルの Webhook URL を登録してください。")
        sys.exit(1)

    now = datetime.datetime.now(datetime.timezone.utc)
    if scheduled:
        end = latest_digest_end(now)
        heading_date = end
    else:
        end = None
        heading_date = now.astimezone(JST)
    pr_number_raw = os.environ.get("PR_NUMBER", "").strip()
    if pr_number_raw:
        numbers = []
        for token in pr_number_raw.split(","):
            token = token.strip()
            if not token:
                continue
            if not re.fullmatch(r"[0-9]+", token):
                print("::error::PR_NUMBER は数字(例: 58)またはカンマ区切りの数字(例: 53,54,55)で指定してください。不正な値: " + token[:50])
                sys.exit(1)
            if token not in numbers:
                numbers.append(token)
        if not numbers:
            print("::error::PR_NUMBER に有効な番号がありません。")
            sys.exit(1)
        prs, fetched = [], []
        for n in numbers:
            r = run(["gh", "pr", "view", n, "--repo", repo,
                     "--json", "number,title,body,url,author,mergedAt"])
            if r.returncode != 0:
                print("::warning::PR #%s を取得できなかったため飛ばします: %s" % (n, r.stderr[:300]))
                continue
            prs.append((None, json.loads(r.stdout)))
            fetched.append(n)
        if not prs:
            print("::error::指定された PR を1件も取得できませんでした。")
            sys.exit(1)
        print("PR_NUMBER 指定: " + ", ".join("#" + n for n in fetched) + " を対象 (dry_run=%s)" % dry_run)
    else:
        if scheduled:
            since = end.astimezone(datetime.timezone.utc) - datetime.timedelta(hours=hours)
        else:
            since = now - datetime.timedelta(hours=hours)
        r = run(["gh", "pr", "list", "--repo", repo, "--state", "merged", "--base", "develop",
                 "--limit", "100", "--search", "merged:>=" + since.strftime("%Y-%m-%dT%H:%M:%SZ"),
                 "--json", "number,title,body,url,author,mergedAt"])
        if r.returncode != 0:
            print("::error::gh pr list に失敗しました: " + r.stderr[:500])
            sys.exit(1)

        prs = []
        for pr in json.loads(r.stdout):
            merged = datetime.datetime.fromisoformat(pr["mergedAt"].replace("Z", "+00:00"))
            author = pr.get("author") or {}
            login = author.get("login", "")
            if scheduled:
                # 半開区間 (since, end]
                if merged <= since or merged > end:
                    continue
            elif merged < since:
                continue
            if author.get("is_bot") or login.endswith("[bot]") or login.startswith("app/"):
                continue
            prs.append((merged, pr))
        prs.sort(key=lambda x: x[0])

        if not prs:
            print("対象期間(直近 %d 時間)に develop へマージされた PR はありません。何も投稿しません。" % hours)
            return
        print("対象 PR: %d 件 (dry_run=%s)" % (len(prs), dry_run))

    # PR 本文由来の @everyone / @here などを無効化する。
    allowed = {"parse": []}
    header = "# 📅 %s の進捗" % heading_date.strftime("%Y-%m-%d")
    print("[header] " + header)

    card_mode = (os.environ.get("DIGEST_FORMAT", "") or "card").strip().lower() != "text"
    print("DIGEST_FORMAT: " + ("card" if card_mode else "text"))
    if not card_mode and not dry_run:
        post(webhook, {"content": header, "allowed_mentions": allowed})

    items = []
    with tempfile.TemporaryDirectory() as tmp:
        for _, pr in prs:
            body = strip_comments(pr.get("body") or "")
            login = (pr.get("author") or {}).get("login", "unknown")
            desc = build_description(body)

            attach, links, originals, total = [], [], [], 0
            can_compress = bool(shutil.which("ffmpeg") and shutil.which("ffprobe"))
            candidates = extract_videos(body)
            if len(candidates) > MAX_VIDEO_CANDIDATES:
                print("  PR #%d: 動画URLが %d 件あるため先頭 %d 件のみ調べ、残りは無視します: %s"
                      % (pr["number"], len(candidates), MAX_VIDEO_CANDIDATES,
                         ", ".join(u for u, _ in candidates[MAX_VIDEO_CANDIDATES:])))
                candidates = candidates[:MAX_VIDEO_CANDIDATES]
            for i, (url, is_video) in enumerate(candidates):
                tag = "  PR #%d %s: " % (pr["number"], url)
                dest = os.path.join(tmp, "pr%d_%d.bin" % (pr["number"], i))
                pok, pctype, psize, preason = probe(url, dest + ".hdr")
                if pok:
                    if not pctype.startswith("video/"):
                        print(tag + "skip (not video: %s)" % (pctype or "unknown"))
                        continue
                    is_video = True
                elif not is_video:
                    print(tag + "skip (probe failed: %s, not judged as video)" % preason)
                    continue
                else:
                    print(tag + "probe failed (%s), fallback to section/extension judgement" % preason)
                compressed = False
                if psize is not None and psize > MAX_VIDEO_BYTES:
                    if not can_compress:
                        links.append(url)
                        print(tag + "too large (%d bytes), ffmpeg not found -> link" % psize)
                        continue
                    if len(attach) >= MAX_VIDEOS_PER_PR:
                        links.append(url)
                        print(tag + "attachment limit reached (%d bytes) -> link" % psize)
                        continue
                    ok, reason, ctype = download(url, dest, COMPRESS_SRC_MAX_BYTES, COMPRESS_DOWNLOAD_TIMEOUT)
                    if not ok:
                        links.append(url)
                        print(tag + "download failed (%s) -> link" % reason)
                        continue
                    src_size = os.path.getsize(dest)
                    cdest = dest + ".c.mp4"
                    ok, reason = compress_video(dest, cdest)
                    os.remove(dest)
                    if not ok:
                        links.append(url)
                        print(tag + "compress failed (%s) -> link" % reason)
                        continue
                    dest, ctype, compressed = cdest, "video/mp4", True
                    print(tag + "compressed (%d -> %d) attach" % (src_size, os.path.getsize(dest)))
                else:
                    ok, reason, ctype = download(url, dest)
                    if not ok:
                        links.append(url)
                        print(tag + "download failed (%s) -> link" % reason)
                        continue
                size = os.path.getsize(dest)
                if len(attach) < MAX_VIDEOS_PER_PR and total + size <= MAX_VIDEO_BYTES:
                    total += size
                    ext = ".mp4" if compressed else video_ext(url, ctype or pctype)
                    attach.append((dest, "pr%d_%d%s" % (pr["number"], i + 1, ext)))
                    if compressed:
                        originals.append(url)
                    else:
                        print(tag + "attach (%d bytes)" % size)
                else:
                    links.append(url)
                    print(tag + "attachment limit reached (%d bytes) -> link" % size)

            if card_mode:
                items.append({"pr": pr, "login": login, "desc": desc, "links": links, "originals": originals,
                              "files": [(path, os.path.splitext(name)[1], os.path.getsize(path)) for path, name in attach]})
                print("[PR #%d] %s / 添付 %d 件 / リンク %d 件" % (pr["number"], pr["title"], len(attach), len(links)))
                continue

            content = build_content(pr, login, desc, links, originals)
            payload = {"allowed_mentions": allowed, "content": content}
            print("[PR #%d] %s / 添付 %d 件 / リンク %d 件 / content %d 文字"
                  % (pr["number"], pr["title"], len(attach), len(links), len(content)))
            if dry_run:
                print(json.dumps(payload, ensure_ascii=False, indent=2))
                continue
            post(webhook, payload, attach, with_components=False)
            time.sleep(1)

        if card_mode:
            msgs = pack_messages(items, header)
            print("投稿メッセージ数: %d" % len(msgs))
            for n, m in enumerate(msgs, 1):
                payload = {"flags": IS_COMPONENTS_V2, "allowed_mentions": allowed, "components": m["components"]}
                if m["attach"]:
                    payload["attachments"] = [{"id": i, "filename": name} for i, (_, name) in enumerate(m["attach"])]
                print("[message %d/%d] text %d 文字 / 添付 %d 件 / コンポーネント %d 個"
                      % (n, len(msgs), m["text"], len(m["attach"]), m["comps"]))
                if dry_run:
                    print(json.dumps(payload, ensure_ascii=False, indent=2))
                    continue
                post(webhook, payload, m["attach"], with_components=True)
                time.sleep(1)


if __name__ == "__main__":
    main()
