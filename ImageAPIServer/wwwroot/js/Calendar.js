
let isCalendarShown = true;

let isPaused = true;

const imageQueue = [];
let currentImageIndex = 0;
let fetchInterval = null;
let intervalSpeed = 10000;



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


function toggleCalendar() {
    var calendarContainer = document.getElementById('cc');
    var toggleButton = document.getElementById('tb');
    var imageContainer = document.getElementById('id');

    if (!isCalendarShown) {
        // Show the calendar
        isCalendarShown = true;
        toggleButton.innerHTML = "Switch to image display"
        imageContainer.style.display = 'none';
        calendarContainer.style.display = 'flex';
        pause();

    } else {
        // Hide the calendar
        isCalendarShown = false;
        toggleButton.innerHTML = "Switch to calendar"
        imageContainer.style.display = 'flex';
        calendarContainer.style.display = 'none';
        unPause();
    }
}

// Image methods:

function fetchRandomImage() {
    fetch('/api/random_image')
        .then(response => {
            if (!response.ok) throw new Error('Network response was not ok');
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

function togglePause() {
    if (!isPaused) {
        pause();
    } else {
        unPause();
    }
}

function pause() {
    const button = document.getElementById('pauseButton');
    isPaused = true;
    clearInterval(fetchInterval);
    fetchInterval = null;
    button.innerText = "Play";
}

function unPause() {
    const button = document.getElementById('pauseButton');
    isPaused = false;
    startFetching();
    button.innerText = "Pause";
}

function updateMemoryRing() {
    const memoryCircles = document.querySelectorAll('.memoryCircle');

    const ring = document.querySelector('.memoryRing');
    if (memoryCircles.length === 0) return;

    const targetCircle = memoryCircles[currentImageIndex];
    const offsetLeft = targetCircle.offsetLeft;
    const circleWidth = targetCircle.offsetWidth;

    ring.style.left = (offsetLeft + circleWidth / 2 - ring.offsetWidth / 2) + 'px';
}

function increaseSpeed() {
    if (intervalSpeed > 1000) { // don't go below 1 second
        intervalSpeed -= 1000; // faster: decrease interval by 1 sec
        updateSpeedDisplay();
        if (!isPaused) startFetching();
    }
}

function decreaseSpeed() {
    if (intervalSpeed < 30000) { // don't go above 30 second
        intervalSpeed += 1000; // faster: decrease interval by 1 sec
        updateSpeedDisplay();
        if (!isPaused) startFetching();
    }
}

function updateSpeedDisplay() {
    const speedDisplay = document.getElementById('speedDisplay');
    speedDisplay.innerText = (intervalSpeed / 1000) + "s";
}


// Start fetching
function startFetching() {
    if (fetchInterval !== null || isPaused) {
        clearInterval(fetchInterval);
    }
    if (!isPaused) {
        console.log("pain");
        fetchInterval = setInterval(fetchRandomImage, intervalSpeed);
        
    }
}

function init() {
    updateSpeedDisplay();
    fetchRandomImage();
}


function ProgressBar(miliseconds) {
    const bar = document.getElementById('progressBar');
    const container = bar.parentElement;

    bar.style.width = '0px'; // reset bar width

    const totalDuration = miliseconds; // milliseconds
    const containerWidth = container.clientWidth;
    const startTime = performance.now();

    function animate(time) {
        const elapsed = time - startTime;
        const progress = Math.min(elapsed / totalDuration, 1);
        const width = containerWidth * progress;
        bar.style.width = width + 'px';
        if (progress < 1) {
            requestAnimationFrame(animate);
        }
    }
    requestAnimationFrame(animate);
}






init();