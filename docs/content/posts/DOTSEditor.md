+++
title= "Unity DOTS Editor"
author= ["Menno"]
tags=[ "dots","ecs","csharp","beginner"]
categories=["dots","ecs","unity"]
series = ["Unity's DOTS"]
date= 2020-02-23T10:59:02+01:00
description="Getting started with Unity DOTS Editor"
+++

## Introduction
Knowing how the Unity editor can support you while developing with DOTS is important to speed up the workflow and get debugging information. In this post I will go over what editor features are available for DOTS. 

To follow along with this post you will needed the following packages.
* **Entities:** Installing this package and its dependencies will add everything needed to develop with DOTS, such as the burst compiler and job system.
* **DOTS Editor:** While optional, this package will add extra editor features for DOTS which will be covered here.

## Entity Debugger
Can be found under: `Window > Analysis > Entity Debugger`

{{< figure src="/DOTSEditor/entityDebugger.png">}}  
The entity debugger gives information about the state of the world. It can show you which entities exist, what components they contain, which systems are running on them and which chunks are used. We will go over each part of the entity debugger.

**1. World selection:**  
Allows you to select the world to show the containing entities and systems of. You can select `Show Full Player Loop` to show all worlds and `Show Inactive Systems` to also show systems that are not running.

**2. System details:**  
This section will allow you to view all system groups and the systems they contain. Notice that systems are listed in order of execution.  
For each system it gives useful performance information in milliseconds spend on the main thread. You can also disable individual systems to prevent them from running. Selecting a system will show you the entities and components the system runs on, and allows you to view chunk information. 

**3. Entity inspector:**  
This shows you all the entities that match the specified system query.  
Notice that components are colored based on `read only`, `read write` or `subtractive` (can not contain), but expect this to be expanded on in the future as currently not all query types have a color code.  
Selecting an entity will show you its component values in the inspector, shown here in the image on the right.

**4. Chunk info:**  
Chunks in DOTS can be very confusing at first, so to properly explain this section I will first give an simplified explanation of chunks. A chunk is a pre-allocated 16kB block of memory, which has an archetype that defines which components are in the chunk. Based on this the chunk calculates how many entities of this archetype it can hold. When entities are added they are put in the chunk until its full at which point a new chunk is created.  
  
The chunk info section shows you how many archetypes match a system query and how many chunks each archetype has. At the bottom it than shows how full these chunks are in a histogram.  

{{< figure src="/DOTSEditor/chunkInfo.png" width=250 >}}
In this image we match 1 archetype which has 3 chunks. A chunk of this archetype can hold 104 entities. 2 chunks are currently full (bar on the right) and 1 chunk holds 13 entities (bar on the left). 

## Live Link Mode
Can be found under: `DOTS > Live Link Mode`

{{< figure src="/DOTSEditor/liveLink.png" width=250 >}}  
Live link allows to convert to DOTS while in edit mode. For live link to work you need to add your objects in a subscene: `Hierarchy > Right click > New SubScene From Selection`. Selecting an object in the subscene will you its ECS components, and the object can now be found in the entity debugger. Keeping the subscene open during play mode will now also allow you to make changes to the entity without restarting.

There are two scene view modes for live link. `SceneView: Live Game State` makes the scene view show the final converted result in edit mode turning the scene into a hybrid rendered scene. `SceneView: Editing State` will instead keep the editor renderer and not apply the full conversion. This mode will renderer gizmos, but any changes to the entity that happen at conversion will not be applied.

## Burst Inspector
Can be found under: `Jobs > Burst > Open Inspector...`

{{< figure src="/DOTSEditor/burstInspector.png">}}  
The burst inspector is a useful tool for low level performance optimizations. Most people won't be able or need to read the compiler output. However the `LLVM Optimization Diagnostics` can still be interesting to look at. It gives compiler info in a more human readable manner, which can be used to for instance check if your code is getting vectorized.

{{< figure src="/DOTSEditor/burstBadCode.png">}}
Here you can see that the compiler marked line 35 as unable to vectorize. 

{{< figure src="/DOTSEditor/burstGoodCode.png">}}
After a change in the code line 35 is marked as vectorized.

## Others
To further control the compilation process you can enable and disable multiple setting having to do with debugging and safety under `Jobs`. These speak for themselves so I won't be going in depth any further.

{{< figure src="/DOTSEditor/enterPlayModeSettings.png">}}  
To speed up the load time when pressing play in the editor, you can disable scene and domain reload under: `Edit > Player Settings > Editor > Enter Play Mode Settings`. This is not directly connected to DOTS in any way. But it's main downsides largely don't apply to DOTS code, while still giving you the benefits of improved speed. For more information see the [documentation](https://docs.unity3d.com/2019.3/Documentation/Manual/ConfigurableEnterPlayMode.html).