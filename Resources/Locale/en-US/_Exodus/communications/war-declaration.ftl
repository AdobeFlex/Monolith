# Pairwise faction war declarations
tsf-comms-computer-circuitboard-name = TSFMC communications computer board
tsf-comms-computer-circuitboard-description = A computer printed circuit board for a TSFMC communications console.

war-declaration-console-title = Interfactional relations
war-declaration-console-available = Declarations of war are available.
war-declaration-console-available-in = Declarations of war will be available in {$time}.
war-declaration-console-status-cold = Relations with {$target}: COLD WAR
war-declaration-console-status-declared = {$source} → {$target}: TOTAL WAR
war-declaration-console-button = Declare war on {$target}
war-declaration-console-confirm = Declare total war on {$target}? This action is irreversible and a truce cannot be concluded.

war-declaration-no-access = You are not authorized to declare war from this console.
war-declaration-too-early = War may not be declared yet. Time remaining: {$time}.
war-declaration-already-active = These factions are already at war.
war-declaration-invalid-target = This faction cannot be selected as a target of war.
war-declaration-failed = The declaration of war could not be processed.
war-declaration-round-not-running = War may only be declared during an active round.
war-declaration-success = {$declarer} has declared war on {$target}.

war-declaration-announcement-sender = Sector Diplomatic Monitoring
war-declaration-announcement = ATTENTION! The faction “{$declarer}” has declared total war on the faction “{$target}”. Civilians are advised to stay away from their military installations until the conflict ends.
war-declaration-ended-announcement = By decision of sector administration, the total war declared by “{$declarer}” against “{$target}” has ended.
war-declaration-cleared-all-announcement = By decision of sector administration, all active faction wars have ended.

war-declaration-pda-entry = [color=crimson]{$declarer} → {$target}[/color]

cmd-setwarlevel-hint-2 = [declarer]
cmd-setwarlevel-hint-3 = [target]
cmd-setwarlevel-invalid-faction = Unknown war faction: {$faction}.
cmd-setwarlevel-same-faction = A faction cannot declare war on itself.
cmd-setwarlevel-already-active = This faction pair is already at war.
cmd-setwarlevel-not-active = This faction pair is not at war.
cmd-setwarlevel-state-unavailable = The sector war state is unavailable.
cmd-setwarlevel-failed = Failed to change the faction war state.
cmd-setwarlevel-success-declared = {$declarer} has declared war on {$target}.
cmd-setwarlevel-success-ended = The war declared by {$declarer} against {$target} has ended.
