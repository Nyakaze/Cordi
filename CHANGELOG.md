## [test-version-v1](https://github.com/Nyakaze/Cordi/compare/test-chatbox-v5...test-version-v1) (17-10-2026)

### General
 - **Fixed:** Known characters were capped at 1000 and then re-looked-up on the Lodestone; all are kept now.
 - **Changed:** Known characters moved to their own file, keeping settings lean.
 - **Changed:** Player avatar/Lodestone lookups are now shared across Chatbox, Peeper, Emote Log, Party and Discord.
 - **Fixed:** Failed Discord deliveries from Peeper/Emote Log are now logged in the Log tab.

### Emote Log
 - **Added:** Configurable overlay entry limit, emote-back hold duration, and its own unhide-on-emote-back switch.
 - **Added:** Fully customizable overlay line with per-piece formatting and colour. -> Watchers -> Emote Log -> Overlay Display
 - **Fixed:** Broken test-button target id, Discord reaction freeze, overlay stutter, and wrong turn direction/dropped hold when emoting back repeatedly.

### Peeper
 - **Fixed:** Visibility plugin integration could freeze/crash the game; now uses the proper API.
 - **Added:** "Unhide players hidden by Visibility" setting with configurable stay-visible duration.
 - **Added:** Fully customizable overlay line, same as Emote Log. -> Watchers -> Peeper -> Overlay Display
 - **Fixed:** Manually hidden players stay hidden, unhidden players revert on their own, and the Visibility whitelist no longer gets polluted.
 - **Changed:** Renamed consistently to "Peeper" throughout settings (migrates automatically).

### Combined Overlay
 - **Added:** Own on/off switch, drag-to-corner layout (replacing left/right swap), in-page watcher status and toggles.
 - **Fixed:** Combined window alone no longer leaves both halves empty when "detect while closed" is off.

### ChatBox
 - **Added:** GIF picker (GIPHY search/trending, favourites, remembered searches), recent-DMs button with search, per-channel Server Rail colour, and ESC-clears-input toggle.
 - **Fixed:** Chat keybind focus after logout, and a duplicate scrollbar in Server Rail/Channel List.

### Activity
 - **Added:** Emote titles with custom rules and display duration, self-targeting for testing, and PatMe/PatMeHonorific import.

## [test-chatbox-v5](https://github.com/Nyakaze/Cordi/compare/test-chatbox-v4.2...test-chatbox-v5) (16-09-2026)

### ChatBox
 - **Added:** New "Global Read option" -> Chatbox -> Mentions -> Notifications
 - **Added:** Arrow up and down in the input walk through the messages you already sent, like the vanilla chat does. The last 50 are kept and shared between the chatbox and the conversation windows.
 - **Added:** New "Colour Messages by Channel" option. The message text takes the colour of its chat type like the game chat does, so a party message is party coloured. Party Icons colours stay on the name. -> Chatbox -> Appearance -> Messages -> Colour Messages by Channel
 - **Fixed:** "Send Tell" in the game's own right click menu now opens the conversation in Cordi instead of filling in the vanilla chat.
 - **Fixed:** Player name completion kept suggesting names after the target was already typed out. It now stops once the name is complete, and matching works across the whole name instead of one word at a time.
 - **Fixed:** Picking a command like /wave from the completion ran it but left the text in the input, so the next Enter ran it a second time. One Enter now completes, runs and clears it.
 - **Fixed:** Messages took up more room than their text needed. The emote size no longer stretches rows without emotes, the hover box now ends where the message ends, and the avatar never makes a row taller than the message. With Message Spacing and Line Spacing at 0 the lines now sit directly on top of each other. -> Chatbox -> Appearance -> Message Spacing / Line Spacing

### Advertisement Filter
 - **Fixed:** The pattern list could end up empty and never filled itself again. The default patterns are restored once on update. Patterns you added yourself stay, and nothing is added twice.

### Translation
 - **Changed:** The free service is now Bing + Google instead of the old Google endpoint that ran into rate limits. DeepL is the new default, and without an API key the free Bing and Google services answer.
 - **Added:** Chat noise filter. Things like "hahaha", "o7", "gg" or "lol" no longer cost a request. Clicking translate yourself still works. -> Chatbox -> Translation -> Scope -> Skip Chat Noise
 - **Added:** Local language detection through Lingua, with a confidence threshold and a per channel language memory, so short words are no longer mistaken for random languages. -> Chatbox -> Translation -> Languages -> Detection Confidence
 - **Changed:** Language detection is set to "Local first" once on update, so existing setups get the fewer requests as well. Switch it back to "Always online" and it stays that way. -> Chatbox -> Translation -> Languages -> Language Detection
 - **Added:** Option to skip relayed Discord messages. -> Chatbox -> Translation -> Scope -> Translate Discord Messages
 - **Fixed:** Manual translations are no longer silently dropped while a rate limit is active. They now skip the queue and report a failure if they really fail.
 - **Fixed:** The rate limit backoff decays again instead of staying high for the rest of the session.


## [test-chatbox-v4.2](https://github.com/Nyakaze/Cordi/compare/2.3.0...test-chatbox-v4.2) (14-09-2026)

### General
 - **NEW UI:** Completely reworked the UI of Cordi.
 - **Added:** ChatBox Feature
 - **Added:** ChatBox Messanger

### ChatBox
 - **Info:** As its the first automated release and I already said whats in there, im not gonna put all of it here again

## [2.3.0](https://github.com/Nyakaze/Cordi/compare/2.2.0...2.3.0) (26-05-2026)

### Player Track Tab
 - **REMOVED:** Completly removed

### Peeper
 - **Added:** Outer Glow for Peeper Text

### Activity
 - **Added:** Custom Activitys presets.
 - **Fixed:** A bug where Custom Activitys cleared sometimes.
 - **Fixed:** A bug where Custom Activitys where not clearing correctly.

### Discord Chat
 - **Updated:** More emote parsing

## [2.2.0](https://github.com/Nyakaze/Cordi/compare/2.0.5...2.2.0) (26-05-2026)

### Player Track Tab
 - **Added:** A new Tab called "Player Track".
 - **Added:** Option to track player changes like Name, World, Free Company, etc.

### Discord Activity
 - **Fixed:** A bug where Lightless thinks its a Link you want promote.

### Discord Chat
 - **Added:** Emote Parsing. Emotes will now be shown in Game when sent through Discord.

## [2.1.2](https://github.com/Nyakaze/Cordi/compare/2.0.4...2.0.5) (29-04-2026)

### General
 - **UPDATE:** API15 Update

## [2.0.5](https://github.com/Nyakaze/Cordi/compare/2.0.4...2.0.5) (25-04-2026)

### Slash Commands
 -**Added:** All Emotes beeing avaiable through Discord Commands.
 -**Added:** Custom Commands which will be executed ingame when called through Discord.
 -**Added:** Option to enable/disable Custom Commands and Groups through Discord.

### Activitys
 - **Fixed:** Some bugs where prioritys were set wrong and blacklisting not working.
 - **Added:** Placeholder for small and large image Texts.

### Peeper
 - **Fixed:** A bug where the context menu text had the wrong color.

## [2.0.4](https://github.com/Nyakaze/Cordi/compare/2.0.2...2.0.4) (16-04-2026)

### Activitys
 - **Updated:** Time related placeholders now update title correctly
 - **Changed:** Recognition of Activitys to be more accurate.

### Log Tab
 - **Added:** Log Tab to see Plugin Logs. Visible when pressed Ctrl + Shift + L while Cordi window is selected.

## [2.0.2](https://github.com/Nyakaze/Cordi/compare/2.0.1...2.0.2) (14-03-2026)

### General
 - **Fixed:** A bug where the targetdot had a fixed offset. Not scaling with characters size

## [2.0.1](https://github.com/Nyakaze/Cordi/compare/2.0.0...2.0.1) (14-03-2026)

### Windows
 - **Updated:** Windows can now be resized without a size restriction.

### Chats
 - **Fixed:** A bug where the Chat Mappings were not working.

## [2.0.0](https://github.com/Nyakaze/Cordi/compare/1.2.1...2.0.0) (14-03-2026)

### Overhaul
 - **Overhauled** the entire Config Window.

### Chats
 - **Added:** A new Tab called "Chats" to the Config Window.

### Peeper
 - **Added:** Options for Transparency
 - **Added:** Option for Direction Arrow
 - **Added:** Option for Distance
 - **Added:** Option for Showing Peepers Target
 - **Added:** Option for Change highlight color
 - **Added:** Option for Target Dot
 - **Added:** Option for Hide Titlebar

### Emote Log
 - **Added:** Options for Transparency
 - **Added:** Option for Hide Titlebar

### Combined Window
 - **Added:** A new Tab called "Combined" to the Config Window.

### Activitys
 - **Added:** Option for Gradient Titles
 - **Added:** Option to Blacklist certain Activitys

### Settings
 - **Added:** Option to change the fontsize

## [1.2.1](https://github.com/Nyakaze/Cordi/compare/1.2.0...1.2.1) (11-02-2026)

### Party
 - **Fixed:** A bug where Cross Realm Parties were not working in Party Track Feature.
 - **Added:** A Summary of the Party when the Party fills.

## [1.2.0](https://github.com/Nyakaze/Cordi/compare/1.1.6...1.2.0) (09-02-2026)

### Remember Me
 - **Fixed:** Item Icons not showing up in the Examine Tab.
 - **Updated:** Examine Glamour window to look nicer.
 - **Added:** Colors for Glamour Slots
 - **Added:** Try On feature
### Peeper
 - **Added:** Better default sound when a player is peeping.
 - **Added:** Option to disable Notifications in Combat.
 - **Added:** Option to disable Sounds in Combat.
 - **Fixed:** A bug where the Discord Notification didnt update if the peeper looked away to fast.
### Chats
 - **Added:** Option to get notified in a different Channel when u get a tell.
### Party
 - **Fixed:** A bug where an Error was thrown, when a player had no Logs.



## [1.1.6](https://github.com/Nyakaze/Cordi/compare/1.1.5...1.1.6) (08-02-2026)

### Chat
 - **Added:** ExtraChat Plugin Support.

## [1.1.5](https://github.com/Nyakaze/Cordi/compare/1.1.4...1.1.5) (08-02-2026)

### Discord
 - **Fixed:** Reactions sometimes failing.
### General
 - **Fixed:** A lot of under the hood changes.
### UI
 - **Added:** "WIP" Text to Examine Feature.
 - **Updated:** View of Remembered and Examined People.

## [1.1.4](https://github.com/Nyakaze/Cordi/compare/1.1.3...1.1.4) (07-02-2026)

### General
 - **Added:** Experimental Warning for Discord Commands.
 - **Added:** Tooltip for Chat Mappings.
 - **Added:** Stats to the Config Window.
 - **Fixed:** Tooltips not beeing in the top right corner.
 - **Updated:** Discord Bot Button to be disabled when the bot is starting or stopping.
 - **Changed:** Cordi Peep window to Peeper window.
 - **Changed:** Discord Bot Token input to hide the token.


## [1.1.3](https://github.com/Nyakaze/Cordi/compare/1.1.2...1.1.3) (06-02-2026)

### Webhook Service
 - **Fixed:** Webhooks sometimes throwing Errors. Should now reliably send messages.
