#!/bin/bash

# Loop through the IDs from 1 to 217
for id in {1..217}; do
    echo "$id"
    # Check if directory with the name of the current ID exists, if not, create it
    if [ ! -d "$id" ]; then
        mkdir "$id"
    fi
    # Convert the DDS file to cropped GIFs
    convert "dragitem$id.dds" -crop 40x40 +repage +adjoin "$id/item_%02d.gif"
done

# Set the base counter value
baseCounter=500

# Check if the 'out' directory exists, if not, create it
if [ ! -d "out" ]; then
    mkdir "out"
fi

# Loop through the directories from 1 to 217
for i in {1..217}; do
    # Get a list of files in the current directory
    files=("$i"/*)

    # Initialize file counters
    fileCounter=0
    counter=$baseCounter

    # Loop through each file in the directory
    for file in "${files[@]}"; do
        # Skip '.' and '..' and files smaller than 100 bytes
        if [[ "$file" == "." || "$file" == ".." || $(stat -c%s "$file") -lt 100 ]]; then
            continue
        fi

        # Calculate the new file name
        name=$((fileCounter + counter))

        # Copy the file to the 'out' directory with the new name
        cp "$file" "out/item_$name.gif"

        # Increment the file counter by 6
        fileCounter=$((fileCounter + 6))

        # If the file counter reaches or exceeds 31, reset it and increment the counter
        if ((fileCounter >= 31)); then
            fileCounter=0
            counter=$((counter + 1))
        fi
    done

    # Increment the base counter by 36 after processing each directory
    baseCounter=$((baseCounter + 36))
done
