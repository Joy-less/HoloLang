# Holo

## Example

```rb
# FizzBuzz #

fizzbuzz = (n) {
    for (range(1, 10), (i) {
        if (i.mod(3).equals(0) .and (i.mod(5).equals(0)), () {
            log("FizzBuzz");
        })
        .elseif (i.mod(3).equals(0), () {
            log("Fizz");
        })
        .elseif (i.mod(5).equals(0), () {
            log("Buzz");
        })
        .else (() {
            log(n);
        });
    });
};
```

## Design

### Boxes

A box is a fundamental object in Holo. You can create one with curly braces (`{}`).
```rb
my_box = {};
```

Boxes have a dictionary of named variables and values.

Some boxes have an external data object, for example a string or a number, which is used for primitive types.

Boxes have a method which is called when the box is called (unless `get` is defined). The method has a tuple representing the list of parameters, and an expression evaluated when the method is called.

Every box belongs to an actor, which prevents the box being used concurrently.

<!--
Boxes contain:
- Actor: Every box belongs to an actor.
- Data: An external object, for example a string.
- Variables: A dictionary of string names and box values.
- Method: A method called when the box is called (unless `get` is defined).
  - Parameters: A tuple representing the list of parameters.
  - Expression: An expression evaluated when the method is called.
-->

### Methods

A box can be defined with a method which is evaluated when the box is called.
```rb
my_box = () {
    log("hello");
};
my_box();
```

This method can be overridden with a variable called `get`.
```rb
my_box = {
    get = () {
        log("hello");
    };
};
my_box();
```

### Tuples

A tuple is a syntax structure that contains a list of tuple elements. You can create one with brackets (`()`).
```rb
# Pass a tuple of arguments #
list(1, 2, 3);

# Assign a tuple of variables to a tuple of values #
(a, b, c) = (1, 2, 3);

# Assign a tuple of variables to the values of a box #
(a, b, c) = list(1, 2, 3);

# Create a method with a tuple of parameters and an expression #
print = (value) {
    log(value);
};

# Pass a tuple of a single argument #
list 1;

# Create a method with a tuple of a single parameter and an expression #
print = value {
    log(value);
};
```

You can spread an element using two dots (`..`).
```rb
(1, ..list(2, 3), 4)
```

You can name an element using a colon (`:`).
```rb
(a: 1, b: 2, 3)
```

You can put a tuple inside a tuple.
```rb
(1, (2, 3), 4)
```

### Expressions

An expression is an instruction with a resulting value.

#### Multi Expression

A list of expressions separated by semicolons (`;`).
```rb
a; b; c
```

#### Get Expression

Gets the value of a variable, with an optional target expression.
```rb
my_variable
```
```rb
target.my_variable
```

#### Assign Expression

Assigns the value of a variable, with an optional target expression.
```rb
my_variable = value
```
```rb
target.my_variable = value
```

#### Compound Assign Expression

Assigns the value of a variable using the current variable value, with an optional target expression.
```rb
my_variable = .add(1)
```
```rb
target.my_variable = .add(1)
```

#### Call Expression

Calls the target expression with a tuple or box of arguments.
```rb
log("hi")
```

#### Box Expression

Creates a box with a method, running an optional expression in the box.
```rb
{
    name = "John Doe";
    health = 9_999_999;
}
```

#### String Expression

Creates a string box with an array of UTF-8 bytes.
```rb
"hello 世界"
```

#### Integer Expression

Creates an integer box with a 64-bit signed integer.
```rb
42
```

#### Real Expression

Creates a real box with a 64-bit double.
```rb
42.0
```

#### External Call Expression

Calls an external method with a tuple or box of arguments.
```rb
external_method(1, 2, 3)
```

### Components

Every box has a sequence of components, which includes itself at the start and `Box` at the end.

If a box has a variable called `components`, this is added to the sequence of components in a breadth-first search.

This can be used for Object-Oriented Programming (OOP) and multiple-inheritance.
```rb
animal = {
    favorite_food = "bread";
};
meowing_object = {
    meow = () {
        log("meow!");
    };
};
cat = {
    components = list(animal, meowing_object);

    favorite_food = "fish";

    log(favorite_food); # fish #
    meow(); # meow! #
};
```

### Exceptions

Exceptions can be thrown with `throw` and caught with `catch`. These are used for control flow.
```rb
result = catch (break_loops, () {
    for (range(1, 10), (i) {
        for (range(1, 10), (j) {
            if (i.equals(5) .or (j.equals(3)) {
                throw(break_loops);
            });
        });
    });
});
```

### Type Annotations

A variable's type can be annotated with a tuple after its name. These annotations are ignored at runtime.
```rb
numbers list.of(int) = list(1, 2, 3);

number (integer, null) = null;
```