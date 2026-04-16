module TheCelesteTracker_ModSampleTrigger

using ..Ahorn, Maple

@mapdef Trigger "TheCelesteTracker_Mod/SampleTrigger" SampleTrigger(
    x::Integer, y::Integer, width::Integer=Maple.defaultTriggerWidth, height::Integer=Maple.defaultTriggerHeight,
    sampleProperty::Integer=0
)

const placements = Ahorn.PlacementDict(
    "Sample Trigger (TheCelesteTracker_Mod)" => Ahorn.EntityPlacement(
        SampleTrigger,
        "rectangle",
    )
)

end