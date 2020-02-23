+++
title= "Linking entities or entity relationships"
author= ["Niels","Simon"]
tags=[ "dots","ecs","csharp","reference","relation","hybrid"]
categories=["dots","ecs","unity"]
series = ["Unity's DOTS"]
date= 2019-11-21T14:59:02+01:00
description="How to link entities in a 'hierarchy'."

+++

## The problem

Often in games different objects interact with each other, eg. a button opens a door, a child follows its parent etc. With the new ESC system those hierarchies aren't trivial, hierarchies don't really exist natively. So how do we link entities in a hierarchy?



## Linking entities as a group

In the new ESC if you translate a GameObject via ` ConvertToEntity ` to entities the hierarchy would not be translated into the ESC World. The simple solution to this is called `LinkedEntityGroup`. This translates the hierarchy to ESC land. 

`LinkedEntityGroup` is a Buffer which can be used to link the lifetime of entities together.  This buffer takes an entity as reference point (root) and when this one gets destroyed all the linked entities get destroyed as well.

The following example demonstrates the usage of the `LinkedEntityGroup`:

```csharp
public class LinkedEntityGroupComponent: MonoBehaviour, IConvertGameObjectToEntity
{
     public void Convert (Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
     {
         var buffer = dstManager.AddBuffer <LinkedEntityGroup> (entity);

         var children = transform.GetComponentsInChildren <Transform> ();
         foreach (var child in children)
         {
             var childEntity = conversionSystem.GetPrimaryEntity (child.gameObject);
             buffer.Add (childEntity);
         }
     }
 }
```

