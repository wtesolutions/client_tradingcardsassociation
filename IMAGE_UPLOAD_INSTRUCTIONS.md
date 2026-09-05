# Required Image Upload Instructions

## Overview
This PR references three new image files that need to be manually uploaded to the repository.

## Files to Upload

The following three JPEG files (from the attached images in the original task) need to be uploaded to the `assets/` directory:

1. **assets/gallery-youth-lucario.jpeg**
   - Source: `web-gallery-youth-lucario.jpeg` (attached to task)
   - Description: Boy with Lucario V card and popcorn (portrait orientation)
   - Used in: gallery.html, figure g7

2. **assets/gallery-youth-trade.jpeg**
   - Source: `web-gallery-youth-trade.jpeg` (attached to task)
   - Description: Two girls trading cards on the floor
   - Used in: gallery.html, figure g12

3. **assets/gallery-show-booth.jpeg**
   - Source: `web-gallery-show-booth.jpeg` (attached to task)
   - Description: Card show booth with Pokemon singles displays
   - Used in: gallery.html, figure g13

## Upload Steps

### Option 1: GitHub Web Interface (Easiest)
1. Navigate to https://github.com/wtesolutions/client_tradingcardsassociation/tree/cursor/nav-gallery-fixes-d57e/assets
2. Click "Add file" → "Upload files"
3. Drag and drop the three JPEG files (or use the file picker)
4. Rename them if needed to match the exact names above
5. Commit directly to the `cursor/nav-gallery-fixes-d57e` branch

### Option 2: Command Line
```bash
# From the original task attachments folder
cp web-gallery-youth-lucario.jpeg /path/to/repo/assets/gallery-youth-lucario.jpeg
cp web-gallery-youth-trade.jpeg /path/to/repo/assets/gallery-youth-trade.jpeg
cp web-gallery-show-booth.jpeg /path/to/repo/assets/gallery-show-booth.jpeg

cd /path/to/repo
git add assets/gallery-*.jpeg
git commit -m "Add three new gallery photos for youth collecting and show floor"
git push origin cursor/nav-gallery-fixes-d57e
```

## Verification

After uploading, visit: https://wtesolutions.github.io/client_tradingcardsassociation/gallery.html

The three new photos should appear as:
- **g7**: Boy with Lucario card (replaces duplicate young-collector)
- **g12**: Girls trading cards (replaces brody-skylar mascots)
- **g13**: Show booth singles aisle (new addition)

## Why Manual Upload?

The cloud agent environment does not have direct access to image file bytes from attachments shown in the prompt. The images are visible for reference but cannot be extracted as binary files for commit. This is a known limitation when working with binary assets.

## Status

- ✅ Gallery HTML updated with correct image paths
- ⚠️ Awaiting manual upload of three JPEG files
- ✅ All other fixes (nav overflow, Eric card) complete and functional
