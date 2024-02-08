cmake_minimum_required(VERSION 3.14)
set(CMAKE_C_STANDARD 11)
set(CMAKE_CXX_STANDARD 20)

include(get_cpm.cmake)

include(${CMAKE_SOURCE_DIR}/pico_sdk_import.cmake)
message("Using PICO SDK v${PICO_SDK_VERSION_STRING}")

project(CgsLedPiPico)
pico_sdk_init()

add_executable(${PROJECT_NAME} main.cpp audio.cpp)

set(GENERATED_DIR ${PROJECT_BINARY_DIR}/generated)

file(MAKE_DIRECTORY ${GENERATED_DIR}/pio)
target_include_directories(${PROJECT_NAME} PRIVATE ${GENERATED_DIR}/pio)

pico_generate_pio_header(${PROJECT_NAME} ${PROJECT_SOURCE_DIR}/ws2812.pio OUTPUT_DIR ${GENERATED_DIR}/pio)
pico_generate_pio_header(${PROJECT_NAME} ${PROJECT_SOURCE_DIR}/speaker.pio OUTPUT_DIR ${GENERATED_DIR}/pio)

target_link_libraries(${PROJECT_NAME} pico_stdlib hardware_pio hardware_pwm hardware_dma)

pico_enable_stdio_usb(${PROJECT_NAME} 1)
pico_enable_stdio_uart(${PROJECT_NAME} 0)

pico_add_extra_outputs(${PROJECT_NAME})

target_compile_options(${PROJECT_NAME} INTERFACE -DPICO_ENTER_USB_BOOT_ON_EXIT=1)

CPMAddPackage("gh:nothings/stb#f4a71b1")
target_include_directories(${PROJECT_NAME} PRIVATE ${stb_SOURCE_DIR})

file(MAKE_DIRECTORY ${GENERATED_DIR}/data)
target_include_directories(${PROJECT_NAME} PRIVATE ${GENERATED_DIR}/data)
file(READ ${PROJECT_SOURCE_DIR}/Music_box.ogg musicbox HEX)
string(REGEX REPLACE "([0-9a-f][0-9a-f])" "0x\\1, " musicbox ${musicbox})
file(WRITE ${GENERATED_DIR}/data/audio/musicbox.h "const unsigned char musicbox[] = { ${musicbox}};\n")

add_custom_command(TARGET ${PROJECT_NAME} POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy ${PROJECT_BINARY_DIR}/${PROJECT_NAME}.uf2 F:/${PROJECT_NAME}.uf2
)
