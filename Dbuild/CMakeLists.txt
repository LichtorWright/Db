cmake_minimum_required(VERSION 3.15)

# ── Project name ──────────────────────────────────────────────────────────
# The project folder name is derived from the .dbproj file.
# When scaffolding with `dbuild -new project MyGame`, the folder is MyGame/
# and the manifest is MyGame.dbproj. Update DB_PROJECT_NAME to match.
set(DB_PROJECT_NAME "Project" CACHE STRING "Name of the Db project (must match .dbproj filename)")

project(${DB_PROJECT_NAME} VERSION 1.0.0 LANGUAGES CXX)

# Set C++ standard
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Output directories
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)
set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)

# Project folder path
set(DB_PROJECT_DIR "${CMAKE_SOURCE_DIR}/${DB_PROJECT_NAME}")

# Find all generated .g.cpp files
file(GLOB GENERATED_SOURCES "${DB_PROJECT_DIR}/Pregen/*.g.cpp")

# Find all hand-written C++ files
file(GLOB MANUAL_SOURCES 
    "${DB_PROJECT_DIR}/Source/*.cpp"
    "${DB_PROJECT_DIR}/Included Libraries/Headers/*.h"
)

# Create executable
add_executable(${DB_PROJECT_NAME}
    ${GENERATED_SOURCES}
    ${MANUAL_SOURCES}
)

# Include directories
target_include_directories(${DB_PROJECT_NAME} PRIVATE
    ${CMAKE_SOURCE_DIR}
    "${DB_PROJECT_DIR}/Included Libraries/Headers"
    "${DB_PROJECT_DIR}/Pregen"
)

# Compiler warnings
if(MSVC)
    target_compile_options(${DB_PROJECT_NAME} PRIVATE /W4 /WX-)
    # Disable specific warnings if needed
    target_compile_options(${DB_PROJECT_NAME} PRIVATE /wd4100) # unreferenced formal parameter
else()
    target_compile_options(${DB_PROJECT_NAME} PRIVATE -Wall -Wextra -Wpedantic)
endif()

# Debug/Release configurations
if(CMAKE_BUILD_TYPE STREQUAL "Debug")
    if(MSVC)
        target_compile_options(${DB_PROJECT_NAME} PRIVATE /Zi /Od)
        target_link_options(${DB_PROJECT_NAME} PRIVATE /DEBUG)
    else()
        target_compile_options(${DB_PROJECT_NAME} PRIVATE -g -O0)
    endif()
    message(STATUS "Building Debug configuration")
else()
    if(MSVC)
        target_compile_options(${DB_PROJECT_NAME} PRIVATE /O2)
    else()
        target_compile_options(${DB_PROJECT_NAME} PRIVATE -O3)
    endif()
    message(STATUS "Building Release configuration")
endif()

# Print configuration summary
message(STATUS "Project: ${DB_PROJECT_NAME}")
message(STATUS "Project dir: ${DB_PROJECT_DIR}")
message(STATUS "Generated sources: ${GENERATED_SOURCES}")
message(STATUS "Manual sources: ${MANUAL_SOURCES}")
