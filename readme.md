## CoApp

CoApp is currently undergoing a bit of a cleanup on GitHub.

This is the "core" project. There is a solution in here which can be built without the Windows SDK/DDK but 
following the instructions in https://github.com/coapp/coapp.org/wiki/Setting-Up-Your-CoApp-Development-Environment 
is still recommended.

If anything doesn't make sense or documentation looks incomplete - log an issue https://github.com/coapp/coapp.org/issues.


### Building this project

Once you've cloned the repository (git clone git@github.com:coapp/coapp.git) you should be able to build it from the command line using `pTk` (included in a submodule):

``` batch
cd coapp

tools\ext\ptk build release 
```

or open one of the `.SLN` files in Visual Studio:

`coapp.sln` -- contains the projects without the tricky-to-build prerequisites (native dlls and bootstrappers)

or 

`coapp-with-prerequisites.sln` -- contains the projects **with** the tricky-to-build prerequisites (native dlls and bootstrappers)

It is not recommended that you use this project, the prerequisite DLLs must be digitally signed to work correctly (which is why the signed copies are shipped in the ext/binaries submodule)