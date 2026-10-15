# Claude Code hooks (2026-10-16): the agent never commits on its own, and the user never jumps through a hoop.
#
#   record  (UserPromptSubmit)  stores the user's latest message for this session.
#   guard   (PreToolUse, Bash and PowerShell)  lets a git command that changes the repository (commit, push,
#           reset, rebase, merge, tag, revert, cherry-pick, restore, clean, stash, checkout, am) run only when
#           the user's latest message asks for that very action by name ("commit", "push", ...), not negated
#           ("don't commit"). Anything else is denied, in every permission mode. Reads (status, diff, log) pass.
#
# The user's word is the permission, exactly as the rule was given: "commit" said for the change at hand.
import json
import os
import re
import sys
import tempfile

VERBS = "commit|push|reset|rebase|merge|tag|revert|cherry-pick|restore|clean|stash|checkout|am"
GUARDED = re.compile(r"\bgit\b[^|;&\n]*\b(" + VERBS + r")\b")
NEGATED = re.compile(r"\b(don'?t|do not|not|never|no|without|stop|instead of)\b(\W+\w+){0,2}\W+\b(commit|push|reset|rebase|merge|tag|revert|cherry-pick|restore|clean|stash|checkout|am)", re.I)


def prompt_file(session):
    return os.path.join(tempfile.gettempdir(), "claude-last-prompt-" + re.sub(r"[^\w-]", "_", session or "none") + ".txt")


def main():
    mode = sys.argv[1] if len(sys.argv) > 1 else "guard"
    try:
        data = json.load(sys.stdin)
    except ValueError:
        data = {}
    session = data.get("session_id") or ""
    if mode == "record":
        with open(prompt_file(session), "w", encoding="utf-8") as f:
            f.write(data.get("prompt") or "")
        return
    command = (data.get("tool_input") or {}).get("command") or ""
    match = GUARDED.search(command)
    if not match:
        return
    verb = match.group(1)
    try:
        with open(prompt_file(session), encoding="utf-8") as f:
            prompt = f.read()
    except OSError:
        prompt = ""
    asked = re.search(r"\b" + re.escape(verb) + r"\w*", prompt, re.I) is not None and not NEGATED.search(prompt)
    if asked:
        return  # the user asked for it in their latest message: the command runs
    print(json.dumps({
        "hookSpecificOutput": {
            "hookEventName": "PreToolUse",
            "permissionDecision": "deny",
            "permissionDecisionReason": (
                "Blocked: 'git " + verb + "' changes the repository and the user's latest message does not ask for a "
                + verb + ". The agent must not do this on its own; it runs only after the user asks for it by name."
            ),
        }
    }))


main()
