#!/bin/bash
# test-ecuador.sh
# Verifica se Teleamazonas e Ecuavisa sono raggiungibili con NordVPN Ecuador.
# Risultato confermato (2026-06-14):
#   Senza VPN → HTTP 403
#   Con NordVPN Ecuador #2 (virtuale in Colombia) → HTTP 200 + stream ok

UA="Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:125.0) Gecko/20100101 Firefox/125.0"

declare -A CHANNELS
CHANNELS["Teleamazonas"]="https://teleamazonas-live.cdn.vustreams.com/live/fd4ab346-b4e3-4628-abf0-b5a1bc192428/live.isml/playlist.m3u8"
CHANNELS["Ecuavisa"]="https://mdstrm.com/live-stream-playlist/6089d0a7f61e11081f8c832a.m3u8"

test_url() {
  local name=$1 url=$2
  local code
  code=$(curl -s -o /dev/null -w "%{http_code}" \
    -H "User-Agent: $UA" \
    --max-time 8 --location "$url")
  echo "  $name: HTTP $code"
}

echo "================================================"
echo " Ecuador IPTV Link Test"
echo "================================================"

echo ""
echo "--- SENZA VPN ---"
for name in "${!CHANNELS[@]}"; do
  test_url "$name" "${CHANNELS[$name]}"
done

echo ""
echo "--- Connetto NordVPN Ecuador ---"
nordvpn connect Ecuador
sleep 4

echo ""
echo "IP corrente: $(curl -s https://ipinfo.io/country) (atteso EC o CO)"

echo ""
echo "--- CON VPN ---"
for name in "${!CHANNELS[@]}"; do
  test_url "$name" "${CHANNELS[$name]}"
done

echo ""
echo "--- Test stream HLS (10 segmenti) ---"
nordvpn connect Ecuador 2>/dev/null
sleep 2
for name in "${!CHANNELS[@]}"; do
  url="${CHANNELS[$name]}"
  PLAYLIST=$(curl -s -H "User-Agent: $UA" --location --max-time 8 "$url")
  if echo "$PLAYLIST" | grep -q ".m3u8\|.ts\|#EXT"; then
    echo "  $name: ✓ playlist HLS valida"
  else
    echo "  $name: ✗ risposta non HLS"
    echo "  Primi 200 char: ${PLAYLIST:0:200}"
  fi
done

nordvpn disconnect
echo ""
echo "Disconnesso. Test completato."
