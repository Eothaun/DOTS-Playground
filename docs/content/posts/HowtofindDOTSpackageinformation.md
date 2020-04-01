+++
title= "How to find more information about the DOTS packages? "
author= ["Simon"]
tags=[ "dots","ecs","csharp","beginner"]
categories=["dots","ecs","unity"]
series = ["Unity's DOTS"]
date= 2020-04-01T10:59:02+01:00
description="This article will show you a overview of how you can find out more about Unity's DOTS, Burst, C# Job System internals. This is very useful when the Documentation does not help you further."

+++

## Do you want to learn more about DOTS internals?
I hope you find your way to this article with the search for answers. I will show you quickly how to find out more about Unity and its internals. This is a great source of inspirations. As a game development student at Breda University of applied sciences I could apply the gained knowledge from just researching the files besides in my DOTS project also in my own C++ project. From the Unity source code you can learn a great deal of general application of Data Oriented Design or multithreading.

My teammate [Menno](https://www.mennomarkus.com/) learned a great deal from the files when the documentation stopped during his journey of creating his own native containers! In case you have not read it go and check it out! [Custom Native Container Part 1: The Basics](https://dotsplayground.com/2020/03/customnativecontainerpt1/)



## Where to start?

When ever you have a Unity project and you have access to the source code *if it is a C# only project* . DOTS and its related packages are written in `HPC#` ([High-performance C#](https://blogs.unity3d.com/2019/02/26/on-dots-c-c/)) Unity's own subset. This means we have access to its internals!

### Where would they be? 

When ever we download a package in Unity they are stored at the same location `D:\DOTS\Library\PackageCache` (replace `D:\DOTS` with your own project). In there you have typically a large or small list of packages depending on your project.

The ones which are important are for DOTS and Burst:

- `com.unity.burst@*`
- `com.unity.dots.editor@*`
- `com.unity.entities@*`
- `com.unity.jobs@*`
- `com.unity.collections@*` - For `NativeContainer`

Additional maybe you want to have a look into:

- `com.unity.mathematics@*`
- `com.unity.physics@*`



### What can you find in there?

In those packages you can find the entire source code of DOTS and it makes it easier to understand or do research on how things are implemented. Alternative you can use this also as a source of reference when there is no documentation.

In there you can find gems like SIMD instructions. Just check ` com.unity.burst/Runtime/x86/` . This will lead you to:

![image-20200401221904286](/howto/simd.png)

[Official Documentation]( https://docs.unity3d.com/Packages/com.unity.burst@1.3/api/Unity.Burst.Intrinsics.X86.Sse.html)

### How to search in there?

You could argue now that you could use VS and press F12 or inspect code to see the implementation of DOTS/Burst and co? This does not work as I have to tell you. If you do this you will something like this:

![image-20200401223459115](/howto/entity.png)

But this is helpful because this gives us an indication where we can find the source for this. The first indication is the `namespace` . The namespace tells us in which package we have to look. In this case it will be in `Unity.Entities`which translates to `com.unity.entities@*` .

This leads us to the package and then we have to go to the right folder: for example `D:\git\DOTS-Playground\Library\PackageCache\com.unity.entities@0.6.0-preview.24`  Now I can search for the proper file. This is the hardest task. They are not always quite obvious. 

In this case they are: `Unity.Entities\Types` . Now I have access to the source code:

![image-20200401223748995](/howto/entity_source.png)



## Conclusion

This is a great way to learn much about the internals. It allows you to gather a deep understanding about the architecture. If you are interested  in more about data oriented design in general check out [Data-Oriented Design Book from *Richard Fabian* ](http://www.dataorienteddesign.com/dodmain/).

Soon I will demonstrate how this has helped me understanding the level streaming ([see this great video von Unite](https://www.youtube.com/watch?v=9MuC3Kp6OBU)) and how to implement a rebuild all Entities Caches function together with my teammate [Niels](https://github.com/Nvs2000)