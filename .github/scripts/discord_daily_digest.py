#!/usr/bin/env python3
"""直近 N 時間に develop へマージされた PR の機能紹介を Discord 進捗チャンネルへ投稿する。

discord_daily_digest.yml から呼ばれる。設定はすべて環境変数で受け取る(ユーザー入力をシェルに展開しないため)。
  GH_TOKEN, REPO, DISCORD_PROGRESS_WEBHOOK_URL, HOURS, DRY_RUN, PR_NUMBER, DIGEST_FORMAT
PR_NUMBER(任意): 指定すると期間・マージ状態・base・Bot除外の絞り込みを無視し、その PR 1件だけを
  通常と同じ形式で投稿する(未マージでも可。ローカルでのテスト投稿用)。DRY_RUN と併用できる。
Webhook URL はログに出さない。

投稿形式(DIGEST_FORMAT、既定 card): 1PR = 1メッセージ。
  card: Discord Components V2。flags=32768(IS_COMPONENTS_V2)を付け、content / embeds は使わない。
    1つの Container(左に色の帯が付くカード)に 見出しリンク+作成者 → 概要 → 動画(Media Gallery) の順で入れる。
    送信URLに ?with_components=true を付ける。動画は attachment://videoN.ext で参照する。
    動画を添付できずリンクにフォールバックした場合のみ、見出しの Text Display 末尾に動画URLを貼る。
    全 Text Display の合計は 3800 文字以内(概要を削り、見出し・作成者・動画リンクを優先して残す)。
  text: 従来形式。content(見出し→作成者→概要、2000文字以内)に動画を添付する。
    Components V2 が Webhook で受理されない・動画が再生されない場合の切り替え用。
見出し記法(card / text 共通): 日付メッセージ=H1(`# `)、PR タイトルのリンク=H2(`## `)、概要=H3(`### `)。
日付見出しメッセージは常に通常の content メッセージ。allowed_mentions は常に parse=[]。
"""
import datetime
import json
import os
import re
import subprocess
import sys
import tempfile
import time

# Discord の無料枠の添付上限は 10MB。ペイロード分の余裕を見て少し小さくしておく。
MAX_VIDEO_BYTES = 10_000_000
MAX_VIDEOS_PER_PR = 3
# content の上限は 2000 文字。安全側で短くする。
CONTENT_LIMIT = 1900
# Components V2 の Text Display 合計上限は 4000 文字。安全側で短くする。
CARD_TEXT_LIMIT = 3800
CARD_COLOR = 0x5865F2
IS_COMPONENTS_V2 = 1 << 15
JST = datetime.timezone(datetime.timedelta(hours=9))

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


def build_content(pr, login, desc, links, limit=CONTENT_LIMIT):
    """1PR 分の本文を組み立てる。limit 文字以内。見出し・作成者・動画リンクを優先し、概要を削る。"""
    title = "#%d %s" % (pr["number"], pr["title"])
    heading = "## [%s](<%s>)" % (escape_md(clip(title, 300)), pr["url"])
    head = heading + "\n作成者: " + login
    tail = ("\n\n" + "\n".join(links)) if links else ""
    budget = limit - len(head) - len(tail) - 2
    middle = ("\n\n" + clip(desc, budget)) if desc and budget > 20 else ""
    content = head + middle + tail
    return content if len(content) <= limit else clip(content, limit)


def build_card(pr, login, desc, links, media_names, limit=CARD_TEXT_LIMIT):
    """Components V2 の Container を1つ返す。Text Display の合計は limit 文字以内。"""
    title = "#%d %s" % (pr["number"], pr["title"])
    heading = "## [%s](<%s>)" % (escape_md(clip(title, 300)), pr["url"])
    head = heading + "\n作成者: " + login
    if links:
        head += "\n\n" + "\n".join(links)
    budget = limit - len(head)
    parts = [{"type": 10, "content": head}]
    if desc and budget > 20:
        parts.append({"type": 10, "content": clip(desc, budget)})
    if media_names:
        parts.append({"type": 12, "items": [{"media": {"url": "attachment://" + n}} for n in media_names]})
    return {"type": 17, "accent_color": CARD_COLOR, "components": parts}


def extract_videos(body):
    """(url, 動画と見なせるか) のリスト。実行動画セクション優先、無ければ本文全体から探す。"""
    sec = get_section(body, "実行動画")
    in_section = sec is not None
    text = sec if in_section else body
    urls = []
    for m in list(ASSET_RE.finditer(text)) + list(EXT_RE.finditer(text)):
        u = m.group(0).rstrip(".,;")
        if u not in urls:
            urls.append(u)
    return [(u, in_section or bool(EXT_RE.fullmatch(u))) for u in urls]


def run(cmd):
    return subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")


def download(url, dest):
    """動画をダウンロードし (ok, reason, content_type) を返す。トークンは送らない。"""
    r = run(["curl", "-sSL", "--max-time", "120", "--max-filesize", str(MAX_VIDEO_BYTES),
             "-o", dest, "-w", "%{http_code} %{content_type}", url])
    if r.returncode != 0:
        return False, "download failed or too large", ""
    code, _, ctype = r.stdout.strip().partition(" ")
    if code != "200":
        return False, "http " + code, ctype.lower()
    if os.path.getsize(dest) > MAX_VIDEO_BYTES:
        return False, "too large", ctype.lower()
    return True, "", ctype.lower()


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


def main():
    repo = os.environ["REPO"]
    webhook = os.environ.get("DISCORD_PROGRESS_WEBHOOK_URL", "")
    dry_run = os.environ.get("DRY_RUN", "false").lower() == "true"
    hours_raw = os.environ.get("HOURS", "") or "24"
    if not re.fullmatch(r"[0-9]+", hours_raw) or int(hours_raw) < 1:
        print("::error::hours は 1 以上の整数で指定してください。")
        sys.exit(1)
    hours = int(hours_raw)
    if not webhook and not dry_run:
        print("::error::Secret DISCORD_PROGRESS_WEBHOOK_URL が未設定です。進捗チャンネルの Webhook URL を登録してください。")
        sys.exit(1)

    now = datetime.datetime.now(datetime.timezone.utc)
    pr_number = os.environ.get("PR_NUMBER", "").strip()
    if pr_number:
        if not re.fullmatch(r"[0-9]+", pr_number):
            print("::error::PR_NUMBER は数字(例: 58)で指定してください。")
            sys.exit(1)
        r = run(["gh", "pr", "view", pr_number, "--repo", repo,
                 "--json", "number,title,body,url,author,mergedAt"])
        if r.returncode != 0:
            print("::error::gh pr view に失敗しました: " + r.stderr[:500])
            sys.exit(1)
        pr = json.loads(r.stdout)
        prs = [(None, pr)]
        print("PR_NUMBER 指定: #%s のみ対象 (dry_run=%s)" % (pr_number, dry_run))
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
            if merged < since:
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
    header = "# 📅 %s の進捗" % now.astimezone(JST).strftime("%Y-%m-%d")
    print("[header] " + header)
    if not dry_run:
        post(webhook, {"content": header, "allowed_mentions": allowed})

    card_mode = (os.environ.get("DIGEST_FORMAT", "") or "card").strip().lower() != "text"
    print("DIGEST_FORMAT: " + ("card" if card_mode else "text"))

    with tempfile.TemporaryDirectory() as tmp:
        for _, pr in prs:
            body = strip_comments(pr.get("body") or "")
            login = (pr.get("author") or {}).get("login", "unknown")
            desc = build_description(body)

            attach, links, total = [], [], 0
            for i, (url, is_video) in enumerate(extract_videos(body)):
                dest = os.path.join(tmp, "pr%d_%d.bin" % (pr["number"], i))
                ok, reason, ctype = download(url, dest)
                if ok and (ctype.startswith("image/") or ctype.startswith("text/")):
                    continue  # 動画ではない(スクリーンショット等)
                size = os.path.getsize(dest) if ok else 0
                if ok and len(attach) < MAX_VIDEOS_PER_PR and total + size <= MAX_VIDEO_BYTES:
                    total += size
                    attach.append((dest, "pr%d_%d%s" % (pr["number"], i + 1, video_ext(url, ctype))))
                elif is_video:
                    links.append(url)
                    print("  PR #%d: リンク投稿にフォールバック (%s)" % (pr["number"], reason or "添付上限"))

            if card_mode:
                # attachment:// で参照できる安全な ASCII 名にする
                attach = [(path, "video%d%s" % (n + 1, os.path.splitext(name)[1])) for n, (path, name) in enumerate(attach)]
                names = [name for _, name in attach]
                card = build_card(pr, login, desc, links, names)
                text_len = sum(len(c["content"]) for c in card["components"] if c["type"] == 10)
                payload = {"flags": IS_COMPONENTS_V2, "allowed_mentions": allowed, "components": [card]}
                if attach:
                    payload["attachments"] = [{"id": n, "filename": name} for n, name in enumerate(names)]
                size_info = "text %d 文字" % text_len
            else:
                content = build_content(pr, login, desc, links)
                payload = {"allowed_mentions": allowed, "content": content}
                size_info = "content %d 文字" % len(content)
            print("[PR #%d] %s / 添付 %d 件 / リンク %d 件 / %s"
                  % (pr["number"], pr["title"], len(attach), len(links), size_info))
            if dry_run:
                print(json.dumps(payload, ensure_ascii=False, indent=2))
                continue
            post(webhook, payload, attach, with_components=card_mode)
            time.sleep(1)


if __name__ == "__main__":
    main()
