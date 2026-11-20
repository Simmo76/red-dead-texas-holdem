#!/usr/bin/env python3
"""Set TestFlight "What to Test" on the latest build of the iOS app.

Uses the same App Store Connect API key as the TestFlight lane:
ASC_KEY_ID, ASC_ISSUER_ID, and ASC_KEY_P8_BASE64. Notes are read from
what-to-test.txt beside this script, or from WHAT_TO_TEST when that is set.
"""

from __future__ import annotations

import base64
import json
import os
import sys
import time
import urllib.error
import urllib.request

import jwt

# Bundle id of the TestFlight app. The latest non-expired build receives the notes.
BUNDLE_ID = "com.deadmanshand.app"
API = "https://api.appstoreconnect.apple.com"
NOTES_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "what-to-test.txt")


def load_pem(raw: str) -> str:
    raw = raw.strip().strip('"')
    if "BEGIN PRIVATE KEY" in raw:
        return raw.replace("\\n", "\n")
    decoded = base64.b64decode(raw.replace("\n", "").replace(" ", ""))
    pem = decoded.decode()
    if "BEGIN PRIVATE KEY" not in pem:
        raise SystemExit("ASC_KEY_P8_BASE64 is not an App Store Connect API key")
    return pem


def token() -> str:
    now = int(time.time())
    pem = load_pem(os.environ["ASC_KEY_P8_BASE64"])
    encoded = jwt.encode(
        {
            "iss": os.environ["ASC_ISSUER_ID"],
            "iat": now,
            "exp": now + 15 * 60,
            "aud": "appstoreconnect-v1",
        },
        pem,
        algorithm="ES256",
        headers={"kid": os.environ["ASC_KEY_ID"], "typ": "JWT"},
    )
    return encoded if isinstance(encoded, str) else encoded.decode()


def api(method: str, path: str, body: dict | None = None, bearer: str | None = None):
    data = None if body is None else json.dumps(body).encode()
    req = urllib.request.Request(
        API + path,
        data=data,
        method=method,
        headers={
            "Authorization": f"Bearer {bearer or token()}",
            "Content-Type": "application/json",
        },
    )
    try:
        with urllib.request.urlopen(req) as resp:
            raw = resp.read().decode()
            return resp.status, json.loads(raw) if raw else {}
    except urllib.error.HTTPError as exc:
        detail = exc.read().decode()
        return exc.code, detail


def notes_text() -> str:
    text = os.environ.get("WHAT_TO_TEST", "").strip()
    if not text:
        with open(NOTES_PATH, encoding="utf-8") as handle:
            text = handle.read().strip()
    if not text:
        raise SystemExit(f"No notes in WHAT_TO_TEST or {NOTES_PATH}")
    if len(text) > 4000:
        raise SystemExit(f"What to Test is {len(text)} characters; the limit is 4000")
    return text


def latest_build(bearer: str) -> dict:
    status, apps = api(
        "GET",
        f"/v1/apps?filter[bundleId]={BUNDLE_ID}&limit=1",
        bearer=bearer,
    )
    if status != 200 or not apps.get("data"):
        raise SystemExit(f"Could not find {BUNDLE_ID}: HTTP {status} {apps}")
    app_id = apps["data"][0]["id"]
    status, builds = api(
        "GET",
        f"/v1/builds?filter[app]={app_id}&sort=-uploadedDate&limit=10"
        "&fields[builds]=version,uploadedDate,processingState,expired",
        bearer=bearer,
    )
    if status != 200 or not builds.get("data"):
        raise SystemExit(f"Could not list builds: HTTP {status} {builds}")
    usable = [
        build
        for build in builds["data"]
        if not build["attributes"].get("expired")
        and build["attributes"].get("processingState") != "INVALID"
    ]
    if not usable:
        raise SystemExit("No usable TestFlight build")
    return usable[0]


def marketing_version(build_id: str, bearer: str) -> str:
    status, payload = api("GET", f"/v1/builds/{build_id}/preReleaseVersion", bearer=bearer)
    if status != 200:
        return ""
    return payload.get("data", {}).get("attributes", {}).get("version", "")


def upsert_notes(build_id: str, text: str, bearer: str) -> None:
    status, locs = api("GET", f"/v1/builds/{build_id}/betaBuildLocalizations", bearer=bearer)
    if status != 200:
        raise SystemExit(f"Could not read build localizations: HTTP {status} {locs}")
    existing = locs.get("data") or []
    targets = [loc for loc in existing if str(loc["attributes"].get("locale", "")).startswith("en")]
    if not targets and existing:
        targets = existing[:1]
    if targets:
        for loc in targets:
            code, result = api(
                "PATCH",
                f"/v1/betaBuildLocalizations/{loc['id']}",
                {
                    "data": {
                        "type": "betaBuildLocalizations",
                        "id": loc["id"],
                        "attributes": {"whatsNew": text},
                    }
                },
                bearer,
            )
            if code not in (200, 201):
                raise SystemExit(
                    f"Could not update {loc['attributes'].get('locale')} notes: HTTP {code} {result}"
                )
            print(f"Updated {loc['attributes'].get('locale')} What to Test")
        return
    code, result = api(
        "POST",
        "/v1/betaBuildLocalizations",
        {
            "data": {
                "type": "betaBuildLocalizations",
                "attributes": {"locale": "en-US", "whatsNew": text},
                "relationships": {
                    "build": {"data": {"type": "builds", "id": build_id}}
                },
            }
        },
        bearer,
    )
    if code not in (200, 201):
        raise SystemExit(f"Could not create What to Test: HTTP {code} {result}")
    print("Created en-US What to Test")


def wait_for_ready(bearer: str, minimum_build: int, deadline: float) -> tuple[dict, str]:
    # minimum_build 0: wait for the newest upload to reach VALID.
    # minimum_build N: wait until build N or newer is VALID (CI run_number after upload).
    while True:
        bearer = token()
        build = latest_build(bearer)
        attrs = build["attributes"]
        state = attrs.get("processingState")
        number = int(attrs.get("version") or 0)
        print(
            f"Latest build {attrs.get('version')} uploaded {attrs.get('uploadedDate')} "
            f"is {state}"
        )
        ready = state == "VALID" and (
            minimum_build == 0 or number >= minimum_build
        )
        if ready:
            return build, bearer
        if state == "FAILED" and (minimum_build == 0 or number >= minimum_build):
            raise SystemExit(f"Build {number} failed processing")
        if time.time() >= deadline:
            target = (
                "the newest upload"
                if minimum_build == 0
                else f"build {minimum_build} or newer"
            )
            raise SystemExit(f"{target} was not finished processing before the deadline")
        if minimum_build == 0:
            print("Waiting until the newest upload has finished processing.")
        else:
            print(f"Waiting until build {minimum_build} or newer has finished processing.")
        time.sleep(30)


def main() -> None:
    text = notes_text()
    minimum_build = int(os.environ.get("MIN_BUILD", "0"))
    deadline = time.time() + 20 * 60
    build, bearer = wait_for_ready(token(), minimum_build, deadline)
    upsert_notes(build["id"], text, bearer)
    attrs = build["attributes"]
    version = marketing_version(build["id"], bearer)
    label = f"{version} ({attrs.get('version')})" if version else str(attrs.get("version"))
    print(f"What to Test is posted on {label}")
    # An earlier run can still be writing the previous notes. Post again
    # after it has had time to finish so this text is the one that remains.
    time.sleep(90)
    build, bearer = wait_for_ready(token(), minimum_build, deadline)
    upsert_notes(build["id"], text, bearer)
    attrs = build["attributes"]
    version = marketing_version(build["id"], bearer)
    label = f"{version} ({attrs.get('version')})" if version else str(attrs.get("version"))
    print(f"What to Test is posted on {label}")


if __name__ == "__main__":
    main()
