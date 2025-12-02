# Git Cheatsheet — LabGateway

This concise cheatsheet covers the Git commands used during the recent task (adding `__blobstorage__` to `.gitignore`) and other common repository maintenance commands. Use these locally in the repository root.

---

## 1) Inspect repository state

- Show current branch and whether inside a repo:

```bash
git rev-parse --abbrev-ref HEAD
git rev-parse --is-inside-work-tree
```

- Show current remote(s):

```bash
git remote -v
```

- Show working tree status (unstaged, staged, untracked):

```bash
git status --porcelain
# or for a human-friendly view
git status
```

---

## 2) Find if a path is tracked by Git

- Check whether a file or folder is tracked (exit code 0 if tracked):

```bash
git ls-files --error-unmatch <path> 2>/dev/null
# PowerShell note: redirect stderr differently: 2>$null
```

Example:

```bash
# POSIX
git ls-files --error-unmatch __blobstorage__ 2>/dev/null
# PowerShell
git ls-files --error-unmatch __blobstorage__ 2>$null
```

---

## 3) Ignore files / folders

- Add an entry to `.gitignore` (example to ignore top-level `__blobstorage__` only):

```
/__blobstorage__/
```

- If you want to ignore any folder named `__blobstorage__` anywhere in the tree, omit the leading slash:

```
__blobstorage__/
```

---

## 4) Stop tracking a file/folder already tracked (without deleting locally)

When `.gitignore` is updated, tracked files must be removed from the index to stop them being tracked.

```bash
# remove tracked files from the index but keep them in your working directory
git rm -r --cached __blobstorage__

# commit the change
git add .gitignore
git commit -m "chore(gitignore): add __blobstorage__"
```

After this, Git will no longer track changes in `__blobstorage__`.

---

## 5) Committing and pushing

- Stage and commit changes:

```bash
git add <file1> <file2>
git commit -m "chore(gitignore): add __blobstorage__"
```

- Push to `main` (or prefer creating a branch & PR):

```bash
# push current HEAD to origin/main
git push origin HEAD:main

# preferred workflow: create a branch and push, then open a PR
git checkout -b chore/gitignore-ignore-blobstorage
git push -u origin chore/gitignore-ignore-blobstorage
```

---

## 6) Reverting changes (if needed)

- Undo last local commit but keep changes staged:

```bash
git reset --soft HEAD~1
```

- Remove a file from the working tree and index (delete local file):

```bash
git rm -r __blobstorage__
git commit -m "remove blobstorage"
```

---

## 7) Useful diagnostics & cleanup

- Show which .gitignore rule matches a file (Git 2.8+):

```bash
git check-ignore -v -- __blobstorage__
```

- Show commits that touched a path:

```bash
git log --follow -- __blobstorage__
```

- Clean untracked files (use carefully):

```bash
# show what would be removed
git clean -n -d

# actually remove untracked files and directories
git clean -f -d
```

---

## 8) Pull request workflow (quick)

```bash
# create a topic branch
git checkout -b feat/docs-git-cheatsheet
# commit changes
git push -u origin feat/docs-git-cheatsheet
# Open a PR on GitHub from that branch to main
```

---

## 9) PowerShell specifics

- Redirect stderr to $null when calling git utilities that write to stderr in PowerShell:

```powershell
git ls-files --error-unmatch __blobstorage__ 2>$null
```

- In PowerShell, to check whether a path exists:

```powershell
Test-Path __blobstorage__
```

---

## 10) Safety tips

- Double-check `git status` before committing to avoid accidentally including sensitive files.
- Avoid committing secrets: prefer environment variables and secret stores (KeyVault). Use `git-secrets` or similar pre-commit hooks if needed.
- When updating `.gitignore`, remember the ignore only affects untracked files; remove tracked files from the index to stop tracking.

---

If you want, I can also:
- Commit this file and push it to `main` or a topic branch and open a PR. (I already have the commit privileges in this session.)
- Add a short README section linking to this cheatsheet in `docs/README.md`.

Which of these would you like me to do next?
