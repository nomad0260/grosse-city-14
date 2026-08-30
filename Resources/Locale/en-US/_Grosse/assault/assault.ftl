assault-preset-title = Assault
assault-preset-description = Attackers capture zones in sequence. Wave spawns, respawn tickets, gates between zones.

assault-team-attackers = Attack
assault-team-defenders = Defense
assault-team-counts = Attack { $attackers } / Defense { $defenders }

assault-lobby-header = Assault
assault-lobby-random = Random
assault-lobby-select-team = Team
assault-lobby-select-class = Class
assault-lobby-cost = { $cost } pts
assault-lobby-need-loadout = Pick a team and class, or Random, before Ready.
assault-lobby-team-full = That team is full. Pick the other team or Random.
assault-lobby-queued = You are in the respawn wave queue.
assault-lobby-selected-random = Random loadout selected
assault-lobby-ready-hint = Team and class or Random required

assault-class-rebel-rifle = Rebel
assault-class-rebel-rifle-desc = Rifle, basic kit.
assault-class-rebel-shotgun = Rebel (shotgun)
assault-class-rebel-shotgun-desc = Close-range shotgun.
assault-class-rebel-ar2 = Rebel (AR2)
assault-class-rebel-ar2-desc = Heavy pulse rifle.
assault-class-cp = Civil Protection
assault-class-cp-desc = Pistol and CP gear.
assault-class-cp-smg = CP (SMG)
assault-class-cp-smg-desc = Submachine gun.
assault-class-ota = OTA
assault-class-ota-desc = Overwatch soldier.
assault-class-ota-elite = OTA Elite
assault-class-ota-elite-desc = Elite armor and AR2.

assault-hud-phase-prep = Prep: { $time }
assault-hud-phase-attack = Attack
assault-hud-phase-intermission = Gates: { $time }
assault-hud-phase-ended = Round over
assault-hud-tickets = Tickets  A { $attackers }  /  D { $defenders }
assault-hud-zone = Zone { $zone } / { $total }
assault-hud-round = Round: { $time }
assault-hud-wave-attackers = Attack dead { $dead }/{ $total }
assault-hud-wave-defenders = Defense dead { $dead }/{ $total }
assault-hud-capture = Capture: { $percent }%
assault-hud-wave-hint = Respawn at { $percent }% casualties

assault-class-select-title = Select class
assault-class-select-tickets = Respawn tickets: { $tickets }

assault-announce-prep = Assault: { $time }s prep. Get into position.
assault-announce-attack = Attack has started!
assault-announce-captured = Zone { $zone } captured. Attack +{ $atk } tickets, defense +{ $def }. Gates open in { $delay }s. Next zone: { $next }.
assault-announce-gates = Gates to zone { $zone } are open!
assault-announce-last-point = Attackers captured the last point!
assault-announce-tickets = Attackers are out of respawn tickets!
assault-announce-timeout = Round time expired. Defense held the city.

assault-roundend-attackers = Attackers captured every zone.
assault-roundend-defenders = Defenders held the assault.
assault-roundend-draw = Assault ended.
assault-roundend-tickets = Tickets: attack { $attackers }, defense { $defenders }
assault-roundend-zone = Zone { $zone } / { $total }

cmd-assaultstatus-desc = Show Assault mode status.
cmd-assaultstatus-help = Usage: assaultstatus
assault-cmd-inactive = Assault mode is not active.
assault-cmd-status = Phase: { $phase }; zone { $zone }/{ $total }; tickets A { $atk } D { $def }; players { $players }
assault-cmd-queue = Wave queue: { $queued }

assault-capture-examined = Zone { $zone }: { $percent }% captured
assault-capture-examined-captured = Zone { $zone } captured
assault-gate-tools-blocked = This gate is locked for the assault.
