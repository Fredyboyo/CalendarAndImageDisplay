
/* ====================== Globals ====================== */

let isCalendarShown = true;

let isPaused = true;

const imageQueue = [];
let currentImageIndex = 0;
let totalTime = 10000;
let timeRemaining = 0;

/* ====================== Calendar actions ====================== */

document.getElementById("TimedEvents").scrollTop = (48 * 8);
function adjustHeight(element, minHeight) {
    let scrollHeight = element.scrollHeight;
    if (scrollHeight > minHeight) {
        element.style.height = scrollHeight + "px";
    }
}
function resetHeight(element, minHeight) {
    element.style.height = minHeight + "px";
}

/* ====================== Header actions ====================== */
function toggleCalendar() {
    var calendarContainer = document.getElementById('cc');
    var toggleButton = document.getElementById('tb');
    var imageDisplayer = document.getElementById('imageDisplayer');

    if (!isCalendarShown) {
        // Show the calendar
        isCalendarShown = true;
        toggleButton.innerHTML = "Switch to image display"
        imageDisplayer.style.display = 'none';
        calendarContainer.style.display = 'flex';
        pause();
    } else {
        // Hide the calendar
        isCalendarShown = false;
        toggleButton.innerHTML = "Switch to calendar"
        imageDisplayer.style.display = 'flex';
        calendarContainer.style.display = 'none';
        play();
        updateMemoryRing();
    }
}

/* ====================== Image actions ====================== */
function fetchRandomImage() {
    fetch('/api/random_image')
        .then(response => {
            if (!response.ok) {
                pause();
                throw new Error('Network response was not ok');
            }
            console.log("Fetched, paused: " + isPaused + ", calendarShow: " + isCalendarShown);
            return response.json();
        })
        .then(data => {
            const { fileName, contentType, imageData } = data;
            const imageSrc = `data:${contentType};base64,${imageData}`;

            imageQueue.push({ src: imageSrc, fileName: fileName });

            const memoryCircles = document.querySelectorAll('.memoryCircle');

            if (imageQueue.length > 5) {
                imageQueue.shift();
            }
            else {
                memoryCircles[imageQueue.length-1].classList += ' imageLoaded';
            }
            currentImageIndex = imageQueue.length - 1;
            displayCurrentImage();
        })
        .catch(error => {
            console.error('Error fetching the image:', error);
        });
}

function displayCurrentImage() {
    updateMemoryRing();
    const imageElement = document.getElementById('randomImage');
    imageElement.style.opacity = 0;
    const fileNameElement = document.getElementById('fileName');

    if (imageQueue.length > 0) {
        const current = imageQueue[currentImageIndex];
        imageElement.src = current.src;
        fileNameElement.textContent = current.fileName;
        imageElement.style.opacity = 1;
    }
}
/* ====================== Slection controls ====================== */
function showImageAt(index) {
    if (0 <= index && index < imageQueue.length) {
        currentImageIndex = index;
        displayCurrentImage();
    }
}
function showPreviousImage() {
    if (0 < currentImageIndex) {
        currentImageIndex--;
        displayCurrentImage();
    }
}

function showNextImage() {
    if (currentImageIndex < imageQueue.length - 1) {
        currentImageIndex++;
        displayCurrentImage();
    }
}

/* ====================== Pause ====================== */
function togglePause() {
    if (!isPaused) {
        pause();
    } else {
        play();
    }
}

function pause() {
    const button = document.getElementById('pauseButton');
    isPaused = true;
    button.innerText = "Play";
}

/* ====================== Memory Ring ====================== */
function updateMemoryRing() {
    const memoryCircles = document.querySelectorAll('.memoryCircle');

    const ring = document.querySelector('.memoryRing');
    if (memoryCircles.length === 0) return;

    const targetCircle = memoryCircles[currentImageIndex];
    const offsetLeft = targetCircle.offsetLeft;
    const circleWidth = targetCircle.offsetWidth;

    const pos = (offsetLeft + circleWidth / 2 - ring.offsetWidth / 2) + 'px';
    console.log((offsetLeft + circleWidth / 2 - ring.offsetWidth / 2));
    console.log(pos);
    ring.style.left = pos;
}

function increaseSpeed() {
    if (totalTime > 1000) { // don't go below 1 second
        per = timeRemaining / totalTime;
        totalTime -= 1000; // faster: decrease interval by 1 sec
        timeRemaining = totalTime * per;
        updateSpeedDisplay();
        if (!isPaused) play();
    }
}

function decreaseSpeed() {
    if (totalTime < 30000) { // don't go above 30 second
        per = timeRemaining / totalTime;
        totalTime += 1000; // faster: decrease interval by 1 sec
        timeRemaining = totalTime * per;
        updateSpeedDisplay();
        if (!isPaused) play();
    }
}

function updateSpeedDisplay() {
    const speedDisplay = document.getElementById('speedDisplay');
    speedDisplay.innerText = (totalTime / 1000) + "s";
}

function play() {
    if (!isPaused) return; // Don't start if it's already running

    const button = document.getElementById('pauseButton');
    isPaused = false;
    button.innerText = "Pause";

    isPaused = false; // Mark as not paused
    lastTimestamp = performance.now(); // Get the initial timestamp
    requestAnimationFrame(updateLoop); // Start the animation loop
}


function updateLoop(timestamp) {
    if (isPaused) return; // Stop the loop if paused

    // Calculate the time elapsed since the last frame
    const deltaTime = timestamp - lastTimestamp;
    timeRemaining -= deltaTime; // Decrease the remaining time

    // Update the display or perform any actions here
    updateRemainingTimeDisplay(timeRemaining);

    // If the time remaining has reached 0 or passed, trigger the action (e.g., fetch a new image)
    if (timeRemaining <= 0) {
        timeRemaining = totalTime; // Reset time remaining
        fetchRandomImage(); // Call your image-fetching function
    }

    // Update the last timestamp for the next frame
    lastTimestamp = timestamp;

    // Keep the animation loop running
    requestAnimationFrame(updateLoop);
}
function updateRemainingTimeDisplay(timeRemaining) {
    document.getElementById('progressBar').style.width = (100 - 100 * (timeRemaining / totalTime)) + '%';
}

function init() {
    updateSpeedDisplay();
    fetchRandomImage();
}

init();