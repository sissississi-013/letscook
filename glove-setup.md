# Smartgloves setup checklist

Source: Rokoko's "Getting Started with your Smartgloves II" + OptiTrack's setup notes (see research-notes.md).

## What you need
- [ ] Both gloves
- [ ] USB-C to USB-A cable (one comes with them; a USB-C hub/adapter for the Mac)
- [ ] **2 power banks, 5V/3A per USB-A output, 10,000+ mAh recommended** (not included)
- [ ] A Wi-Fi network the gloves and Mac share. **A dedicated router works best** (phone hotspot as a quick test); avoid guest/office/university networks (captive portals and client isolation block the gloves). Rokoko recommends **5 GHz, channels 36–48, router in 802.11ac mode (not Wi-Fi 6/ax), IPv4**, with the Mac on Ethernet to the same router. See research/rokoko-streaming.md.
- [ ] **Rokoko Studio Plus or Pro plan** (or the one-time 7-day trial, card required). Free tier can't stream live. Pro ($70/mo) covers everything, including CSV export and trigger messages.
- [ ] macOS: allow Rokoko Studio through the **Firewall** and **Local Network** privacy settings (it may re-ask on each launch)
- [ ] Rokoko account

## Steps
1. [ ] Install **Rokoko Studio** from https://www.rokoko.com/studio (sign in / create account)
2. [ ] Plug **one** glove's hub into the Mac with the cable
3. [ ] Open Studio and accept any **firmware update**
4. [ ] Top right **Connected Devices** → pick the glove → **WiFi settings** tab → enter network name + password. (The receiver IP should be this Mac's IP on that network.)
5. [ ] When the glove shows connected, unplug it from the Mac and plug it into a **power bank**
6. [ ] Repeat steps 2–5 for the second glove
7. [ ] Create an **actor profile** (enter your hand size) and **drag each glove onto it** to pair
8. [ ] Stand in the calibration pose and **calibrate** (Ctrl+Shift+B calibrates all devices)
9. [ ] Test: move each finger on its own and watch the 3D hands
10. [ ] Make a short test recording and export it (free tier) as a backup data path

## Then: live streaming (paid plan)
11. [ ] Livestreaming panel → **Custom Streaming** cogwheel → add receiver: IP `127.0.0.1` (same Mac), port e.g. `14043`, protocol UDP, format JSON v4 (more joints, good for robots), compression off
12. [ ] Tick **Include connection**, enable the receiver, click **Activate**
13. [ ] Run the Python receiver (`receiver.py`) to confirm data arrives
14. [ ] Download the JSON format .txt files from the Custom Streaming support article into this folder

## What actually worked (2026-10-17, xTribe coworking space)
- Studio 2.6.2.0 → updated to 2.6.2.7 first. Studio saw both gloves over USB ("Device is not yet connected to Wi-Fi → Setup").
- **Error 607 "No Access Point found"** with iPhone hotspots: the gloves never saw them in their scan (a hotspot with a Chinese name, a renamed `letscook` one at 2.4 GHz ch 6, and a friend's at 5 GHz ch 149). Don't use non-ASCII Wi-Fi names.
- **xTribe Guest**: visible to the gloves but a guest network (likely client isolation); skipped.
- **xTribe_IoT (2.4 GHz, ch 11, WPA2) worked.** Mac joined too (IP 10.10.10.30), glove settings: xTribe_IoT + password, Receiver IP = the Mac's IP on that network (it turns green in Studio when it matches), WPA2, 2.4 GHz → "Added Wi-Fi device: Actor 2 Right", 66 frames/s.
- **The Receiver IP must be the Mac's IP on the same network.** Studio may suggest a stale one (orange); restart Studio after the Mac changes networks.
- A glove unplugged from USB without a power bank switches off and drops out.
- Studio's log (`~/Library/Application Support/com.RokokoElectronics.RokokoStudio/Production/logs/studio.log`) shows "Added Wi-Fi device" / "device disconnected" lines: handy to check connection without the UI.

## Troubleshooting
- Gloves don't appear → try a phone hotspot; check the Mac and gloves are on the same network; check the macOS firewall isn't blocking Studio
- Drift → recalibrate between sessions
- Mac on Ethernet/other network → make sure the Mac can still reach the gloves' Wi-Fi subnet
