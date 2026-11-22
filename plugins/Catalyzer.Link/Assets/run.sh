#!/bin/bash
# Catalyzer link client. Catalyzer writes this file; don't edit it (it's rewritten whenever the project is linked).
# It gives any shell that can see this repo folder (a chat's Linux VM, WSL, Git Bash) a hidden Windows terminal:
# the command runs on the PC, in PowerShell, in the repo's root folder, with no window. Output streams back here.
#
#   bash .catalyzer/link/run.sh 'dotnet build -c Debug'      run PowerShell, wait, print the output, exit with its code
#   bash .catalyzer/link/run.sh -f tools/snap.ps1 [-- args]   run a script file from the repo (.ps1 or .cmd)
#   bash .catalyzer/link/run.sh --status                      is the link on?
#   bash .catalyzer/link/run.sh --wait <id>                   keep waiting for a request that's still running
#   bash .catalyzer/link/run.sh --cancel <id>                 stop a request (and everything it started)
# options (before the command):
#   -t <s>  timeout on the PC (default 900)        -w <s>  how long to wait here (default 170, then use --wait)
#   -b      background: keeps running and doesn't block the queue (dev servers, watchers); stops when unlinked
#   -s      survive: outlives Catalyzer itself (installing Catalyzer); its output still lands in out/<id>.log
set -u
L="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
timeout=900; wait=170; bg=""; sv=""; file=""

status() {
  local s="$L/STATUS"
  if [ ! -f "$s" ]; then echo "OFF - no STATUS file: click LINK on this project's card in Catalyzer"; return 1; fi
  local first age; first=$(head -1 "$s" | tr -d '\r'); age=$(( $(date +%s) - $(stat -c %Y "$s") ))
  if [ "$first" = "ACTIVE" ] && [ "$age" -lt 120 ]; then echo "ACTIVE (heartbeat ${age}s ago)"; return 0; fi
  if [ "$first" = "ACTIVE" ]; then echo "OFF - Catalyzer isn't answering (last heartbeat ${age}s ago): is it running?"; return 1; fi
  echo "OFF - click LINK on this project's card in Catalyzer"; return 1
}

follow() {   # follow <id> <seconds>
  local id=$1 limit=$2 off=0 t0 sz code
  t0=$(date +%s)
  while :; do
    if [ -f "$L/out/$id.log" ]; then
      sz=$(stat -c %s "$L/out/$id.log")
      if [ "$sz" -gt "$off" ]; then tail -c +$((off + 1)) "$L/out/$id.log" | head -c $((sz - off)) | tr -d '\r'; off=$sz; fi
    fi
    if [ -f "$L/out/$id.done" ]; then
      sleep 0.3
      if [ -f "$L/out/$id.log" ]; then sz=$(stat -c %s "$L/out/$id.log"); [ "$sz" -gt "$off" ] && tail -c +$((off + 1)) "$L/out/$id.log" | tr -d '\r'; fi
      code=$(tr -d '\r\n ' < "$L/out/$id.done")
      echo "[catalyzer] $id: exit $code"
      [[ "$code" =~ ^[0-9]+$ ]] && exit "$code" || exit 1
    fi
    if [ $(( $(date +%s) - t0 )) -gt 12 ] && ls "$L/in/$id."* >/dev/null 2>&1; then
      echo "[catalyzer] $id wasn't picked up: $(status)"; exit 125
    fi
    if [ $(( $(date +%s) - t0 )) -ge "$limit" ]; then
      echo "[catalyzer] $id is still running on the PC. Keep waiting: bash $0 --wait $id"; exit 124
    fi
    sleep 1
  done
}

case "${1:-}" in
  --status) status; exit $? ;;
  --wait) follow "$2" "${3:-$wait}" ;;
  --cancel) : > "$L/in/$2.cancel"; echo "[catalyzer] cancel requested for $2"; exit 0 ;;
esac
while [ $# -gt 0 ]; do
  case "$1" in
    -t) timeout=$2; shift 2 ;;
    -w) wait=$2; shift 2 ;;
    -b) bg=" background"; shift ;;
    -s) sv=" survive"; shift ;;
    -f) file=$2; shift 2; [ "${1:-}" = "--" ] && shift; break ;;
    --) shift; break ;;
    *) break ;;
  esac
done
status >/dev/null || { status; exit 125; }
id="$(date +%Y%m%d-%H%M%S)-$RANDOM"
mkdir -p "$L/in"
if [ -n "$file" ]; then
  # a script from the repo: run it by path (relative to the repo root), with the rest of the line as its arguments
  ext="${file##*.}"; win="${file//\//\\}"
  if [ "$ext" = "cmd" ] || [ "$ext" = "bat" ]; then body="& cmd.exe /d /c \"$win $*\""
  else body="& \".\\$win\" $*"; fi
else
  body="$*"
fi
[ -z "$body" ] && { echo "nothing to run"; exit 2; }
printf '# catalyzer: timeout=%s%s%s\n%s\n' "$timeout" "$bg" "$sv" "$body" > "$L/in/$id.ps1.part"
mv "$L/in/$id.ps1.part" "$L/in/$id.ps1"
echo "[catalyzer] $id: $(echo "$body" | head -1 | cut -c1-160)"
if [ -n "$bg" ]; then
  sleep 4
  [ -f "$L/out/$id.log" ] && tr -d '\r' < "$L/out/$id.log" | tail -20
  [ -f "$L/out/$id.done" ] && { echo "[catalyzer] $id already ended: exit $(tr -d '\r\n ' < "$L/out/$id.done")"; exit 1; }
  echo "[catalyzer] $id runs in the background. Output: .catalyzer/link/out/$id.log   Stop: bash $0 --cancel $id"
  exit 0
fi
follow "$id" "$wait"
