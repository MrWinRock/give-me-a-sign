# Anomaly Animation Setup Guide

## Overview
This guide explains how to set up animations for your anomaly sprite objects that work with the existing Anomaly.cs script.

## Step 1: Create Animation Controller

1. **Create Animator Controller:**
   - Right-click in your `Assets/Animation/` folder
   - Select `Create > Animator Controller`
   - Name it `AnomalyAnimatorController`

## Step 2: Create Animation Clips

### For Movement Animation (StartMove):
1. **Create Animation Clip:**
   - Right-click in `Assets/Animation/` folder
   - Select `Create > Animation`
   - Name it `AnomalyStartMove`

2. **Record Animation:**
   - Select your anomaly GameObject in the scene
   - Open `Window > Animation > Animation`
   - Click the dropdown and select `AnomalyStartMove`
   - Click the red record button
   - Create keyframes for:
     - Position changes (moving toward target)
     - Scale changes (growing/shrinking effect)
     - Rotation (if needed)
     - Sprite color/alpha changes
     - Any other visual effects

### For Idle/Banished Animation:
1. **Create Animation Clip:**
   - Create another animation named `AnomalyIdle`
   - This can be a simple idle state or banishing effect
   - Record keyframes for return to original state or fade out

## Step 3: Setup Animator Controller

1. **Open Animator Controller:**
   - Double-click `AnomalyAnimatorController`
   - This opens the Animator window

2. **Add States:**
   - Right-click in the Animator window
   - Select `Create State > Empty`
   - Name the first state "Idle" (set as default)
   - Create another state "Moving"

3. **Assign Animation Clips:**
   - Select the "Idle" state
   - In Inspector, set Motion to `AnomalyIdle`
   - Select the "Moving" state
   - In Inspector, set Motion to `AnomalyStartMove`

4. **Create Parameters:**
   - In Animator window, click "Parameters" tab
   - Click the "+" button
   - Add Trigger parameter named "StartMove"
   - Add another Trigger parameter named "Idle"

5. **Create Transitions:**
   - Right-click "Idle" state → "Make Transition" → click "Moving" state
   - Select the transition arrow
   - In Inspector, add condition: StartMove (trigger)
   - Set "Has Exit Time" to false for immediate transition
   
   - Right-click "Moving" state → "Make Transition" → click "Idle" state
   - Add condition: Idle (trigger)
   - Set "Has Exit Time" to false

## Step 4: Apply to GameObject

1. **Add Animator Component:**
   - Select your anomaly GameObject
   - Add Component → `Animator`
   - Assign `AnomalyAnimatorController` to Controller field

2. **Configure Anomaly Script:**
   - The script already has the animator reference
   - Make sure `moveTriggerName` is set to "StartMove"
   - Make sure `idleTriggerName` is set to "Idle"

## Animation Ideas for Anomalies

### StartMove Animation:
- **Scale pulsing** - grows and shrinks rhythmically
- **Position wobble** - slight left/right movement while moving forward
- **Color shift** - gradually change to darker/redder tones
- **Rotation** - slight rotation for unnatural movement
- **Alpha flickering** - brief transparency effects

### Idle/Banished Animation:
- **Fade out** - gradually reduce alpha to 0
- **Shrink away** - scale down to 0
- **Spin and fade** - rotation combined with fade
- **Flash and disappear** - quick white flash then fade

## Advanced Tips

1. **Layer Animations:**
   - Use multiple Animator layers for complex effects
   - Base layer for main movement
   - Additional layers for effects (flickering, color changes)

2. **Animation Events:**
   - Add Animation Events to trigger sound effects
   - Add events at specific keyframes in your animations

3. **Sprite Swap Animation:**
   - If using sprite sequences, animate the Sprite property
   - Create keyframes changing the sprite reference

## Testing

1. **Play Mode Testing:**
   - Enter Play Mode
   - Trigger anomaly response to see animations
   - Check that "StartMove" triggers when anomaly begins moving
   - Check that "Idle" triggers when anomaly is banished

2. **Debug Console:**
   - Watch for animation trigger debug messages
   - The script logs when animations are triggered

## Troubleshooting

- **Animation not playing:** Check Animator Controller is assigned
- **Wrong animation:** Verify parameter names match script settings
- **Immediate transition:** Ensure "Has Exit Time" is false for triggers
- **No transition:** Check conditions are set correctly on transitions
