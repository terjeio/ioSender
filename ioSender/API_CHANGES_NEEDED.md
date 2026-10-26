# API Changes Needed for ioSender Fork Compatibility

**Fork:** https://github.com/JasonTitcomb/ioSender  
**Upstream:** https://github.com/terjeio/ioSender  
**Date:** 2025-01-XX

---

## ? FIXED Issues

### 1. RP.Math Namespace (FIXED)
- **Issue:** `using RP.Math;` namespace doesn't exist
- **Location:** `CNC Core\CNC Core\GCodeEmulator.cs`
- **Fix Applied:** Removed namespace, added local `RotatedPoint` struct and `RotateZ()` helper method
- **Status:** ? COMPLETE

### 2. GCArc Constructor (FIXED)
- **Issue:** Old code using 11-parameter constructor, new constructor has 10 parameters
- **Locations:**
  - `CNC Controls\CNC Controls\GCodeRotate.cs` line 143
  - `CNC Controls Probing\CNC Controls Probing\GCodeTransform.cs` line 128
- **Change:** Removed `arc.IsClocwise` parameter (now derived from `Command`)
- **Fix Applied:** Updated both files to use 10-parameter constructor
- **Status:** ? COMPLETE

### 3. GCFlowControl Properties (FIXED)
- **Issue:** `FlowControl` and `Expression` properties were private
- **Location:** `CNC Core\CNC Core\GCodeParser.cs` lines 3648-3649
- **Fix Applied:** Changed to public properties
- **Status:** ? COMPLETE

---

## ? PENDING Issues (API Changes from Upstream)

### 4. GCodeParser.ParseBlock Signature Change
**Priority:** HIGH  
**Locations:**
- `CNC Core\CNC Core\GCodeJob.cs` lines 167, 229

**Old Signature:**
```csharp
bool ParseBlock(ref string line, bool quiet, out uint ln, out bool isComment)
```

**New Signature:**
```csharp
bool ParseBlock(ref string line, bool quiet, out bool isComment)
```

**Change:** Removed `out uint ln` parameter (line number now handled differently)

**Fix Needed:** Update calls to remove `ln` parameter:
```csharp
// OLD:
if (Parser.ParseBlock(ref block, false, out ln, out isComment))

// NEW:
if (Parser.ParseBlock(ref block, false, out isComment))
```

---

### 5. GCodeBlock.BreakAt Property
**Priority:** MEDIUM  
**Locations:**
- `CNC Controls\CNC Controls\JobControl.xaml.cs` line 1276
- `CNC Controls\CNC Controls\GCodeListControl.xaml.cs` line 136

**Issue:** Property exists in `GCodeBlock` class (line 82 of GCodeJob.cs) but compilation errors suggest API mismatch

**Investigation Needed:** 
- Check if property type changed
- Check if property was moved to different class
- Verify the `GCodeBlock` class being referenced is the correct one

---

### 6. GrblViewModel.FeedHoldDisabled Property
**Priority:** MEDIUM  
**Locations:** `CNC Controls\CNC Controls\JobControl.xaml.cs`
- Line 342: `nameof(GrblViewModel.FeedHoldDisabled)`
- Lines 343, 783, 916, 964, 977, 1001, 1078: Property access

**Issue:** Property removed or renamed in upstream

**Investigation Needed:**
- Search upstream for replacement property
- Possible alternatives: `IsFeedHoldEnabled`, `FeedHoldAllowed`, or similar

---

### 7. Tool.Id Property
**Priority:** MEDIUM  
**Locations:**
- `CNC Controls\CNC Controls\ToolView.xaml.cs` line 87
- `CNC Controls\CNC Controls\WorkParametersControl.xaml.cs` line 103

**Issue:** Property removed or renamed in upstream `Tool` class

**Investigation Needed:**
- Check if renamed to `Code`, `Number`, or similar
- Search for `Tool` class definition in upstream

---

### 8. KeypressHandler.AddFunction Method
**Priority:** LOW  
**Locations:**
- `CNC Controls\CNC Controls\JogBaseControl.xaml.cs` lines 156, 157, 161, 162, 166, 167, 171, 172, 176, 177
- `CNC Controls\CNC Controls\DROControl.xaml.cs` lines 105, 107, 109

**Issue:** Method removed or renamed

**Pattern:**
```csharp
keyboard.AddFunction(KeyJogBplus, null);
```

**Investigation Needed:**
- Check if method renamed to `RegisterFunction`, `AddHandler`, etc.
- May be related to keyboard handling refactor

---

### 9. Grbl.SendRealtimeCommand Method
**Priority:** MEDIUM  
**Location:** `CNC Controls\CNC Controls\JobControl.xaml.cs` line 455

**Issue:** Static method removed from `Grbl` class

**Investigation Needed:**
- Check if moved to different class (`Comms`, `GrblViewModel`, etc.)
- Check if signature changed

---

### 10. GrblSpindles.AddDefault Method
**Priority:** LOW  
**Location:** `ioSender\JobView.xaml.cs` line 386

**Issue:** Static method removed or renamed

**Investigation Needed:**
- Check for `GrblSpindles` class changes
- May be related to spindle configuration refactor

---

### 11. GrblInfo.UseLinenumbers Property
**Priority:** MEDIUM  
**Location:** `CNC Controls\CNC Controls\GCode.cs` line 271

**Issue:** Property removed or renamed

**Current Code:**
```csharp
Program.LoadFile(filename, GrblInfo.UseLinenumbers && AppConfig.Settings.Base.AddLineNumbers);
```

**Investigation Needed:**
- Check if moved to `AppConfig.Settings`
- Check if renamed (e.g., `UseLineNumbers`, `AddLineNumbers`)

---

### 12. Commands.G84 Enum Value
**Priority:** LOW  
**Location:** `CNC Controls\CNC Controls\GCodeWrap.cs` line 222

**Issue:** Enum value removed (G84 is right-hand tapping cycle)

**Investigation Needed:**
- Verify if G84 support was removed intentionally
- Check if needs to be handled differently

---

### 13. CameraConfig.CrosshairPos Properties
**Priority:** LOW (Camera feature)  
**Locations:**
- `CNC Controls Camera\CNC Controls Camera\Camera.xaml.cs` lines 78-79
- `CNC Controls Camera\CNC Controls Camera\CameraControl.xaml.cs` lines 130-131

**Issue:** `CrosshairPosX` and `CrosshairPosY` properties missing

**Investigation Needed:**
- Check if consolidated into single `CrosshairPos` Point property
- May be camera plugin API change

---

### 14. GrblViewModel.FsCwd Property
**Priority:** LOW (SD Card feature)  
**Location:** `CNC Controls\CNC Controls\SDCardView.xaml.cs` line 279

**Issue:** File system current working directory property missing

**Investigation Needed:**
- Check if SD card support was refactored
- May be related to file system plugin changes

---

### 15. GrblConstants.CMD_FS_PWD Constant
**Priority:** LOW (SD Card feature)  
**Location:** `CNC Controls\CNC Controls\SDCardView.xaml.cs` line 263

**Issue:** File system "print working directory" command constant missing

**Investigation Needed:**
- Related to `FsCwd` property above
- Check SD card/file system command refactor

---

### 16. GCParameter Type
**Priority:** LOW  
**Location:** `CNC Core\CNC Core\GCodeEmulator.cs` line 832

**Code:**
```csharp
ngcexpr.ReadSetParameter((token as GCParameter).Expression, ref pos);
```

**Issue:** `GCParameter` class not found

**Investigation Needed:**
- Check if class was removed or renamed
- May be related to parameter handling refactor
- Check `NGCExpr` class changes

---

### 17. GCodeJob.LoadFile addLineNumber Parameter
**Priority:** LOW  
**Context:** Variable `addLineNumber` referenced but not in scope (lines 172, 174, 186)

**Issue:** Logic mismatch between parameter handling

**Investigation Needed:**
- The method signature shows `bool addLineNumber = false` parameter
- But code also references it in places where it's out of scope
- May need to review the `Load()` and `AddBlock()` methods

---

## ?? Investigation Strategy

### Step 1: Clone Upstream and Compare
```bash
cd D:\Repos
git clone https://github.com/terjeio/ioSender ioSender-upstream
# Compare files between:
# - D:\Repos\ioSender (your fork)
# - D:\Repos\ioSender-upstream (clean upstream)
```

### Step 2: Check Upstream Commit History
Look for commits that:
- Renamed properties (search for "rename", "refactor")
- Removed features (search for "remove", "deprecate")
- Changed APIs (search for "breaking", "API")

### Step 3: Search Patterns
For each missing API, search upstream for:
```
Property name: FeedHoldDisabled -> search: "FeedHold", "Hold"
Method name: AddFunction -> search: "Function", "keyboard"
```

---

## ?? Recommended Action Plan

### Immediate (to get building):
1. **Fix ParseBlock calls** - Remove `out ln` parameter
2. **Comment out broken features** temporarily:
   - Camera crosshair position code
   - SD card file system code
   - Keyboard AddFunction calls
   - FeedHoldDisabled references

### Short-term (research & fix):
1. **Tool.Id** ? Find replacement property
2. **GrblViewModel.FeedHoldDisabled** ? Find replacement
3. **Grbl.SendRealtimeCommand** ? Find new location

### Long-term (feature parity):
1. Review all commented-out code
2. Implement proper fixes for optional features (Camera, SD card)
3. Test all functionality

---

## ?? Quick Fix Template

To get the solution building quickly, you can:

1. **Comment out** non-critical features
2. **Add TODO comments** with issue numbers
3. **Create GitHub issues** to track each fix

Example:
```csharp
// TODO: Issue #XX - FeedHoldDisabled property removed in upstream
// Need to find replacement property
// IsFeedHoldEnabled = (feedHoldEnable = true) && !model.FeedHoldDisabled;
IsFeedHoldEnabled = feedHoldEnable = true; // Temporary fix
```

---

## ?? Priority Summary

| Priority | Count | Category |
|----------|-------|----------|
| HIGH     | 1     | Core parsing |
| MEDIUM   | 5     | UI features |
| LOW      | 9     | Optional features |

**Total Remaining Issues:** 15

---

## Next Steps

Would you like me to:
1. **Comment out** the non-critical code to get a building solution?
2. **Search upstream** for specific API replacements?
3. **Create a branch** with quick fixes for testing?

Let me know which approach you'd prefer!
