# CCompilerNet

A C- compiler for .Net platform.  
The grammar is taken from here: http://marvin.cs.uidaho.edu/Teaching/CS445/c-Grammar.pdf.

## User Manual

### How to use?

In order to compile a C- program run the compiler from the command line and pass it the path to the .cm file as an argument.  
The result (.exe file) will be saved in the same directory as the source file.

If you want to save the result in a different location use the output flag in the following structure:  
**-output=<filename/path>.exe**

### Side comments

There are some differences from the original language specification that was mentioned earlier:


- There are no break statements support
- There are no string support
- There are no arrays comparison
- There are no static variable support
- There are no min&max operators support
- There is a built in function: print (has similar to .Net Console.WriteLine functionality)
- There is a built in function: put (for getting user input and saving it in a declared variable)
