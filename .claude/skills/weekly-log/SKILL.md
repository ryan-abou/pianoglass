---
name: weekly-log
description: Write this week's Pinnacle log from git and closed issues, then fill this week's row in the "Pinnacle Project Weekly Progress" Google Sheet after my OK.
disable-model-invocation: true
---

Read git log for the last 7 days and the issues closed this week. Write my Pinnacle weekly log in two parts: "What I accomplished" and "What I still need to accomplish". Plain sentences, 6th-grade level, no jargon, no mention of which tasks used AI. Under 120 words.

Then open my Google Sheet "Pinnacle Project Weekly Progress" with the Google Sheets connector. Rows for each week already exist. Find the row whose Week date covers today and fill in its two blank cells. Show me exactly what you'll write and which row, and wait for my OK first. If those cells already have text, show me the old and new text side by side and ask before replacing. Don't change any other rows, and don't delete or add rows unless I ask.

## Project facts

- Commits: `git log --all --since="7 days ago" --author="Ryan" --format="%ad %s" --date=short`
  (`--all`, because work sits on branches until a headset round passes).
- Closed issues: `gh issue list --state closed --search "closed:>=YYYY-MM-DD"`
  with the date 7 days ago. "Still need to accomplish" comes from the To do
  column of https://github.com/users/ryan-abou/projects/2, top first.
- The sheet is found by name with the Google Drive connector's file search, then
  read and written with the Google Sheets connector. Read the header row first to
  find the Week column and the two log columns; never guess column letters.
- Count the words before showing the draft.
