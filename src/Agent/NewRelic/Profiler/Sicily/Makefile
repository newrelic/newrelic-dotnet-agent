BINARY=sicily
SOURCES=$(wildcard *.cpp) $(wildcard ast/*.cpp) $(wildcard codegen/*.cpp)
OBJECTS=$(patsubst %.cpp,%.o,$(SOURCES))
CXXFLAGS=-Wall -O0 -g3 -I$(PWD) -std=c++0x

all: $(BINARY)

$(BINARY): $(OBJECTS)
	$(CXX) -o $@ $(OBJECTS)

%.o: %.cpp Makefile
	$(CXX) -o $@ $(CXXFLAGS) -c $<

clean:
	find . -name '*.o' -exec rm -f {} \; ; \
	rm -f $(BINARY)

.PHONY: clean all

