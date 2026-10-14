#!/usr/bin/env bash
# Creates a new game project from this template.
#
#   scripts/new-game.sh "Game Name" com.yourcompany.gamename [destination]
#
# - Clones the template's current commit (committed files only: no Library/, Builds/, secrets)
#   into destination (default: a sibling folder named after the game).
# - Sets Product Name, iOS bundle ID, a fresh product GUID, version 0.1.0 / build 0.
# - Puts the game name into Docs/SPEC.md.
# - Keeps the template as git remote "template", so template fixes can be merged later:
#     git fetch template && git merge template/<branch>
# - Makes the first commit in the new repo.
set -euo pipefail

die() { echo "error: $*" >&2; exit 1; }

[[ $# -ge 2 && $# -le 3 ]] || die 'usage: scripts/new-game.sh "Game Name" com.company.game [destination]'
NAME="$1"
BUNDLE_ID="$2"
TEMPLATE="$(cd "$(dirname "$0")/.." && pwd)"

[[ "$NAME" =~ ^[A-Za-z0-9][A-Za-z0-9\ .\'!-]*$ ]] || die "game name may only use letters, digits, spaces and . ' ! -"
[[ "$BUNDLE_ID" =~ ^[A-Za-z][A-Za-z0-9-]*(\.[A-Za-z0-9-]+)+$ ]] || die "invalid iOS bundle ID '$BUNDLE_ID' (e.g. com.yourcompany.gamename; no underscores)"
TEMPLATE_BUNDLE_ID="$(perl -0ne 'print $1 if /  applicationIdentifier:\n(?:    .*\n)*?    iPhone: (.*)/' "$TEMPLATE/ProjectSettings/ProjectSettings.asset")"
[[ "$BUNDLE_ID" != "$TEMPLATE_BUNDLE_ID" ]] || die "use the new game's own bundle ID (not the template's $TEMPLATE_BUNDLE_ID)"

SLUG="$(echo "$NAME" | tr '[:upper:]' '[:lower:]' | tr ' ' '_' | tr -cd 'a-z0-9_-')"
DEST="${3:-$(dirname "$TEMPLATE")/$SLUG}"
[[ ! -e "$DEST" ]] || die "destination already exists: $DEST"

command -v git-lfs >/dev/null || die "git-lfs is required (brew install git-lfs && git lfs install)"
git -C "$TEMPLATE" rev-parse --git-dir >/dev/null 2>&1 || die "template is not a git repository"
SHA="$(git -C "$TEMPLATE" rev-parse HEAD)"
BRANCH="$(git -C "$TEMPLATE" rev-parse --abbrev-ref HEAD)"
if [[ -n "$(git -C "$TEMPLATE" status --porcelain)" ]]; then
  echo "warning: template has uncommitted changes; they are NOT included (using commit ${SHA:0:7})." >&2
fi

echo "Creating '$NAME' ($BUNDLE_ID) at $DEST from template ${BRANCH}@${SHA:0:7}..."
git clone --quiet --no-checkout "$TEMPLATE" "$DEST"
cd "$DEST"
git checkout --quiet -B main "$SHA"
git remote rename origin template
git branch --unset-upstream 2>/dev/null || true

# LFS content must be real files, not pointers.
if git lfs ls-files | grep -q ' - '; then
  git lfs pull template >/dev/null || die "could not fetch LFS files from the template"
fi

# Project identity (values passed via env so names never need escaping).
PS=ProjectSettings/ProjectSettings.asset
GUID="$(uuidgen | tr -d '-' | tr '[:upper:]' '[:lower:]')"
NAME="$NAME" BUNDLE_ID="$BUNDLE_ID" GUID="$GUID" perl -0pi -e '
  s/^(  productName: ).*$/$1$ENV{NAME}/m;
  s/^(  productGUID: ).*$/$1$ENV{GUID}/m;
  s/^(  bundleVersion: ).*$/${1}0.1.0/m;
  s/^(  applicationIdentifier:\n(?:    .*\n)*?    iPhone: ).*$/$1$ENV{BUNDLE_ID}/m;
  s/^(  buildNumber:\n(?:    .*\n)*?    iPhone: ).*$/${1}0/m;
' "$PS"
grep -q "^  productName: $NAME$" "$PS" || die "failed to set productName in $PS"
grep -q "^    iPhone: $BUNDLE_ID$" "$PS" || die "failed to set iOS bundle ID in $PS"

NAME="$NAME" perl -pi -e 's/^# Game: .*$/# Game: $ENV{NAME}/' Docs/SPEC.md

git add -A
git commit --quiet -m "Start $NAME from hc_template ${SHA:0:7}

Template: $TEMPLATE ($BRANCH @ $SHA)"

cat <<EOF

Done: $DEST
Next:
  1. Unity Hub > Add > $DEST  (first open imports everything; takes a few minutes)
  2. Fill in Docs/SPEC.md (one core mechanic)
  3. Per-game ad/consent IDs: follow Docs/SDK.md > "Per game"
  4. Open a Claude Code session in $DEST and ask it to build the game from SPEC.md
Template updates later:  git fetch template && git merge template/$BRANCH
EOF
