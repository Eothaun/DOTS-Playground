+++
title= "How Entites could interact"
author= ["Simon"]
tags= ["esc","DOTS","software engineering","dod","data oriented design","ComponentDataFromEntity"]
categories=["Unity","DOTS","ECS","Data Oriented Design"]
series = ["Unity's DOTS"]
date= 2019-11-21T21:45:00+01:00
description="How can we change our mind set away from interactions towards relationships. This article will also discuss the question: What are relationships? Besides this it will scrape the surface of ComponentDataFromEntity as well as EntityCommandBuffers."
draft=false
+++

## How can we implement interaction between entities?
Before we can actually answer that question, we should formulate the actual problem.

## Summary
There are two problems in an ECS when it comes to *interaction* between entities: read and write access. The truth is that interactions *do not really exists, they hide the implementation of the underlaying relationship*. A relationship is then nothing else than the transformation of data. [(More about)](#transfromation-from-interaction-towards-relationships)

To reason about the right tool for creating those transformations, we need to reason about our code and ask ourselves the following five questions:

1. [What data do we operate on?](#how-do-we-design-systems)
2. [What is our domain? What is the possible input for our transformation.](#how-do-we-design-systems)
3. [What is the frequentcy of the data use?](#how-do-we-design-systems)
4. [What are we actually transfroming? How could our algorithm look like?](#how-do-we-design-systems)
5. [How often do we perfrom our transfromation?](#how-do-we-design-systems)

For infrequent [read access](#read-access-componentdatafromentity) we can easily use the `ComponentDataFromEntity` structure. It allows us array like access to the underlying data. It’s not recommended to use this structure for read access because in this case we give up the guaranteed safety of the C# Job System in a multithreaded environment. 

When it comes to [write access](#write-access-entitycommandbuffer) we should consider to make use of the `EnityCommandBuffer`.  This is a great tool to collect a bunch of commands (actions) we want to perform. The buffer can be invoked immediately or deferred, depending on our needs. In the case of `SystemGroups` we can use our own `CommandBuffer `or we can use one of the default ones.

**For more details follow the rest of this post.**

## The problem
When creating interactions between entities we mainly face 2 types of problems: 

1. Read Access: Concrete this means we have to read certain properties from a particular entity (object) and react based on this. In terms of games: An Actor needs to query / know some information from another part of the game. 
For example within a Quest System: Have all tasks been completed?

2. Write access: Concrete this means we have to write certain properties to an particular entity (object). 

## Transformation from *Interaction* towards *Relationships*
In order to start this transformation we should have a quick look at the first principle of *Data Oriented Design*:

> Data is not the problem domain. For some, it would seem that data-oriented design is the antithesis of most other programming paradigms because data-oriented design is a technique that does not readily allow the problem domain to enter into the software so readily. It does not recognize the concept of an object in any way, as data is consistently without meaning [...]  The data-oriented design approach doesn’t build the real world problem into the code. This could be seen as a failing of the data oriented approach by veteran object-oriented developers, as many examples of the success of object-oriented design come from being able to bring the human concepts to the machine, then in this middle ground, a solution can be written in this language that is understandable by both human and computer. The data-oriented approach gives up some of the human readability by leaving the problem domain in the design document, but stops the machine from having to handle human concepts at any level by just that same action — [Data Orinted Design Book Chapter 1.2](http://www.dataorienteddesign.com/)

This helps us to recognize that interactions *do not really exists, they hide the implementation of the underlaying relationship*. A relationship is nothing else then a transformation of data. In case of an ECS the Entity Manager can be seen as a database and the Entity as a Lookup table key which indexes relationships between components. The systems are just here to interpret those relationships and give them meaning. Therefore, a system should only do one job and do this well. Systems perform transformations of data. This allows us to create generic systems which are decoupled and easy to reuse and as such, we should keep the following in mind:

> One of the main design goals for *Data Oriented Design* driven application is to focus on reusability through decoupling whenever possible. Thus the Unix philosophy *Write programs that do one thing and do it well. Write programs to work together — McIlroy* is a good way of expressing what a system should do.

DOTS or any ECS is built with the idea of relationships in mind. When we are writing systems, we transform data from one state to another to give the data meaning. Therefore systems are defining the meaning of the data relationships. This decoupling gives us the flexibility we need to design complex software such as video games. This allows us to modify the behavior later on, without breaking any dependencies.

## How do we design Systems?
To implement the aforementioned relationships, we have to under take a couple of steps. We have to ask the following questions:

**1.**  What data transformations are we going to do and on which data?This question should lead to “what components do we need to create this relationship?” We should always be able to give a reason why we need this data.

**2.** What is our possible domain? (What kind of inputs do we have?)

When we figure this out, we are able to make the right decision later on and can reason about our code how we implement the relationship?

**3.**  How often does the data change?To determine how often we change the data, we go through component by component and discuss how often we change it. This is important to pick the right tool later. Knowing those numbers or tendencies is great for reasoning about possible performance bottlenecks and where we could apply optimizations.

**4.**  What are we actually transforming?

Writing down the algorithm or the constraints of what we are actually doing with our data is a great solution. In order to pick the right tool based on the planned algorithm, we need to consider the **cost** of our algorithm. 

What does **cost** mean? It can mean anything from runtime costs to implementation cost. It is important to first establish what the right criteria are. The costs at the end enables us to reason about the code. 

To pick the right tool, we need to be able to reason about the costs an algorithm costs us.  In some case if we take run time performance as measurement it is okay to have a slow algorithm if we do not execute this frequently but if this is not the case another solution should be considered.

**5.** How often do we execute the algorithm / transformation?

Based on the information we have by defining what data we need for the transformation, it’s quite easy to define the frequency of execution. The total number of entities / objects is known at the time of judgment therefore we can guess how often this might run. Besides this, we have discussed how often we are suspecting the data to be changed, which leads to a transparency, which gives a good idea of the costs of this code.

**IMPORTANT:** When the data changes, the problem changes. Therefore, we have to properly evaluate with the descriptive method the possible outcome and maybe change the implementation.

## Read Access (ComponentDataFromEntity)

In case its required to read from a certain entity, ComponentDataFromEntity is the right tool. This tool allows us to read a specified type (component) of an entity. It is a native container that provides array-like access to components of a specific type, therefore we can easily read the data we need from it. It is a powerful tool to access component data from entities but on the other hand it allows random access and is therefore slow.

**IMPORTANT:**

> You can safely read from ComponentDataFromEntity in any Job, but by default, you cannot write to components in the container in parallel Jobs (including `IJobForEach<T0>` and `IJobChunk`). If you know that two instances of a parallel Job can never write to the same index in the container, you can disable the restriction on parallel writing by adding `NativeDisableParallelForRestrictionAttribute` to the `ComponentDataFromEntity` field definition in the Job struct.
> 
> [Unity Documentation](https://docs.unity3d.com/Packages/com.unity.entities@0.0/api/Unity.Entities.ComponentDataFromEntity-1.html)

Example:
```csharp
//... code
[BurstCompile]
struct MyJob : IJobForEach<MyCmp,Position>{
    [ReadOnly] public ComponentDataFromEntity<Position> data;

    public void Execute([ReadOnly] ref MyCmp mycmp, [ReadOnly] ref Position pos){
        if(!data.Exists(mycmp.Entity)) return;
        Position mycmppos = data[mycmp.Entity];
        //... do some magic
    }
}
///...
protected override JobHandle OnUpdate(...){
    var job = new MyJob(){
         GetComponentDataFromEntity<Position>(true) // true = read only!
    }
    //...
}
```
## Write Access (EntityCommandBuffer)

The right tool for changing data (write access) in the ECS is it to make use of the `EntityCommandBuffer`, in case of an *infrequent change of data*. In a different context, a more value driven approach (direct change) might be more appropriate. 
The Buffer allows us to cache commands and they will be then executed afterwards. If the context is working in a multithreaded environment it’s important to let the ``EntityCommandBuffer` to know about this. This will be done via this `EntityCommandBuffer.Concurrent`.

```csharp
//... code
[BurstCompile]
struct MyJob : IJobForEach<Target>{
    public EntityCommandBuffer.Concurrent buffer;
    public void Execute(Entity entity, int index,[ReadOnly] ref Target target){
        buffer.AddComponent(index,target.Enity,typeof(...));
    }
}
```

Important to realize here is that nothing happens till the moment Playback() gets called. It depends on our needs if we want to invoke this immediately after we have created the buffer and filled or deferred through Unity’s default Buffers. Then we need to keep the sync points of a game in mind. We have 3 system groups: `InitializationSystemGroup SimulationSystemGroup` and `PresentationSystemGroup`. If we do not specify where we want to add our `CommandBuffer`, our command buffer will be automatically added to the `SimulationSystemGroup`. *It is possible to create your own.*

```csharp
//...Code
protected override OnCreate(...){
    m_buffer = world.GetOrCreateSystem<InitializationEntityCommandBufferSystem>();
}
protected override JobHandle OnUpdate(...){
    var job = new MyJob(){
        buffer = m_buffer.CreateCommandBuffer().ToConcurrent()
    }.Schedule(this,inputDepends);

    m_buffer.AddJobHandleForProducer(job);
    return job;
}
```

### Brief Overview of SystemGroups (Default)

- `InitializationSystemGroup` (updated at the end of the `nitialization` phase of the player loop)
- `SimulationSystemGroup` (updated at the end of the `Update` phase of the player loop)
- `PresentationSystemGroup` (updated at the end of the `PreLateUpdate` phase of the player loop)

All of those groups provide 2 command buffers e.g. `BeginPresentationEntityCommandBufferSystem` and `EndPresentationEntityCommandBufferSystem`. This can be used to determine when we want to execute what.




## References 
*This page is mainly based on the following Unity talk: [Options for Entity interaction - Unite Copenhagen](https://www.youtube.com/watch?v=KuGRkC6wzMY)*
{{< youtube KuGRkC6wzMY>}}