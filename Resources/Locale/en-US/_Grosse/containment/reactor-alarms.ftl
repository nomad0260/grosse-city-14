# RTM reactor: warnings and alarm announcements.
# Sources: DarkCoreAlarmSystem.GetMessage, ContainmentConsoleWindow.BuildWarning /
# BuildContainmentWarning / BuildProjectorBreakdown / footer, CoolingBlockCoolantSystem.OnExamined,
# ContainmentStabilizerSystem popups.
# Numbers are passed already formatted (no NUMBER() in this fork).
# Percent values arrive as whole numbers 0-100; the "%" sign is written here, in the text.

## Chat alarm announcements (DarkCoreAlarmSystem), sender is reactor-sender.

reactor-sender = RTM
reactor-assembly-report = RTM assembly: { $summary }

reactor-alarm-overload = RTM. Grid overload. Output { $output } kW, load { $load } kW. Reduce the load or raise core output.
reactor-alarm-unstable = RTM. Stability { $stability }/100, containment { $integrity }/100. Active emitters { $active } of { $total }. Check cartridges, beams and cooling.
reactor-alarm-expanding = RTM. Core expansion { $expansion }/200, stability { $stability }/100. Facility decay in { $seconds } s. Raise stabilization.
reactor-alarm-compressing = RTM. Compression { $compression }, density { $density }/200, field power { $beam }% of { $maxBeam }%. Collapse in { $seconds } s. Lower compression or field power.
reactor-alarm-containment-failure = RTM. Containment lost. Containment { $integrity }/100. Meltdown in { $seconds } s. All personnel leave the compartment.
reactor-alarm-generic = RTM. Reactor parameters out of tolerance.

## Console warning banner (BuildWarning).

reactor-warning-no-core = No link to the core. Check terminal power and distance to the core.
reactor-warning-temperature-critical = CRITICAL TEMPERATURE: equipment is failing. Shut the reactor down immediately.
reactor-warning-temperature-damage = Temperature above 700 K: emitters and support systems are taking damage.
reactor-warning-temperature-emitters = Temperature above 500 K: emitter efficiency is reduced.
reactor-warning-temperature-check = Temperature above 300 K: check the cooling.
reactor-warning-compression-critical = Critical compression. Collapse in { $seconds } s. Immediately reduce emitter intensity or field power.
reactor-warning-compression-high = Compression above normal: { $compression } / { $threshold }. Lower the emitters or swap the Digamma — collapse is not imminent.
reactor-warning-expanding = The core is expanding. Check cartridges and raise emitter stabilization.
reactor-warning-containment-lost = Containment lost. The reactor is about to melt down.
reactor-warning-overload = Output is below grid load. Reduce the load or raise core output.
reactor-warning-unstable = Stability below normal. Check cartridges, emitter power and integrity.
reactor-warning-initialization = Core stabilization in progress.

## Containment panel (BuildContainmentWarning).

reactor-containment-stopped = Reactor stopped. Containment is not required.
reactor-containment-critical = Critical compression. Lower the emitters or swap the Digamma.
reactor-containment-high = Compression above normal. Lower the emitters or swap the Digamma.
reactor-containment-low = Containment below normal. Check the field and cooling.
reactor-containment-nominal = Containment nominal.

## Cooling hints.

reactor-cooling-stopped = Reactor stopped. Cooling is not required.
reactor-cooling-nominal = Cooling keeps up with the current heat load.
reactor-cooling-insufficient = Cooling cannot remove the heat fast enough. Reduce emitter intensity.

## Cooling block examination (CoolingBlockCoolantSystem.OnExamined).

reactor-cooling-block-empty = Tank empty: fill it with coolant from a canister.
reactor-cooling-block-mixture = Mixed coolants in the tank: heat removal is reduced.
reactor-cooling-block-sump-full = Sump full: drain the waste, the block is not cooling.
reactor-cooling-block-no-transfer = The block is not removing heat: check the coolant, tank temperature and contamination.
reactor-cooling-block-pair = One block serves one emitter — keep it close to its emitter.

## Emitter and cartridge warnings (ContainmentStabilizerSystem).

reactor-emitter-cartridge-spent = Cartridge spent. Emitter stopped; a radioactive cartridge was left inside.
reactor-emitter-cartridge-present = A cartridge is already installed. Remove it first.
reactor-emitter-cartridge-depleted = A spent cartridge emits no beam. Recycle it.
reactor-emitter-cartridge-installed = { $cartridge }-cartridge installed.
reactor-emitter-cartridge-removed = Cartridge removed, emitter stopped.
reactor-emitter-repaired = Emitter repaired: { $integrity }% integrity.

## Emitter — cooler pair line on the console dashboard.

reactor-console-emitters-missing = Emitters not found.
reactor-console-no-pairs = No emitter–cooler pairs.
reactor-console-pairs = Pairs: { $pairs }
reactor-console-beams-ok = all beams firing
reactor-console-beams-partial = not all beams firing

## Console footer status lamps.

reactor-footer-core-none = Core: No link
reactor-footer-core-active = Core: Active
reactor-footer-core-off = Core: Stopped
reactor-footer-emitters-none = Emitters: No link
reactor-footer-emitters-nominal = Emitters: Nominal
reactor-footer-emitters-check = Emitters: Check
reactor-footer-cooling-none = Cooling: No link
reactor-footer-cooling-nominal = Cooling: Nominal
reactor-footer-cooling-attention = Cooling: Attention
