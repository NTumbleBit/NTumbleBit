// global onload // global onload function
const solverServerSessionStatesUri = "http://localhost:5000/api/v1/tumblers/0/SolverServerSessionStates";
const promiseServerSessionStatesUri = "http://localhost:5000/api/v1/tumblers/0/PromiseServerSessionStates";

const solverServerSessionStates = {
  WaitingEscrow: 1,
  WaitingPuzzles: 2,
  WaitingRevelation: 3,
  WaitingBlindFactor: 4,
  WaitingFulfillment: 5,
  Completed: 6
}

const promiseServerSessionStates = {
  WaitingEscrow: 1,
  WaitingHashes: 2,
  WaitingRevelation: 3,
  Completed: 4
}

let solvers = [];
let promises = [];

const solversDiv = document.querySelector('.solvers ul');
const promisesDiv = document.querySelector('.promises ul');

(function () {
  fetchEndpoints();
  setTimeout(fetchEndpoints, 1000);
})();

function handleErrors(response) {
  if (!response.ok) {
    throw Error(response.statusText);
  }
  return response;
}

function fetchEndpoints() {
  console.log('fetch');
  fetch(solverServerSessionStatesUri)
  .then(handleErrors)
  .then((res) => res.json())
  .then((data) => solvers = data);

  fetch(promiseServerSessionStatesUri)
    .then(handleErrors)
    .then((res) => res.json())
    .then((data) => promises = data);

  updateDOM();
}



function updateDOM() {
  solversDiv.innerHTML = solvers.map(solver =>
    genSolverComponent(solver)
  ).join('');

  promisesDiv.innerHTML = promises.map(promise =>
    genPromiseComponent(promise)
  ).join('');
}

function genSolverComponent(solver) {
  const status = solver.status;
  const id = solver.escrowedCoin.scriptPubKey;
  return `
    <li>
      <p>${id}</p>
      <label>Progress: ${status}</label>
      <progress value="1" max="6">
    </li>
  `;
}

function genPromiseComponent(promise) {
  const status = promise.status;
  const id = promise.escrowedCoin.scriptPubKey;
  return `
    <li>
      <p>${id}</p>
      <label>Progress: ${status}</label>
      <progress value="1" max="4">
    </li>
  `;
}